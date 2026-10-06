using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Services.Ml;
using AgenticSystem.Infrastructure.Persistence;
using AgenticSystem.Infrastructure.Persistence.Entities;

namespace AgenticSystem.Infrastructure.BackgroundServices;

public class OnnxInferenceBackgroundWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IOnnxInferenceQueue _queue;
    private readonly IOnnxEventBroadcaster _broadcaster;
    private readonly ILogger<OnnxInferenceBackgroundWorker> _logger;
    private readonly OnnxInputLimits _inputLimits;

    public OnnxInferenceBackgroundWorker(
        IServiceProvider serviceProvider,
        IOnnxInferenceQueue queue,
        IOnnxEventBroadcaster broadcaster,
        ILogger<OnnxInferenceBackgroundWorker> logger,
        IOptions<OnnxInputLimits>? inputLimits = null)
    {
        _serviceProvider = serviceProvider;
        _queue = queue;
        _broadcaster = broadcaster;
        _logger = logger;
        _inputLimits = inputLimits?.Value ?? new OnnxInputLimits();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("🚀 ONNX Inference Background Worker started.");

        // 1. Resiliência: Recuperação de jobs interrompidos (Pending/Processing) na inicialização
        try
        {
            await ReenqueueInterruptedJobsAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error during startup recovery of interrupted ONNX inference jobs.");
        }

        // 2. Limpeza física inicial de resultados antigos (> 7 dias)
        await PurgeOldResultsAsync(stoppingToken);
        var lastPurgeTime = DateTime.UtcNow;

        // 3. Loop principal de consumo sequencial (proteção de CPU do host)
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Purga periódica a cada 24 horas
                if ((DateTime.UtcNow - lastPurgeTime).TotalHours >= 24)
                {
                    await PurgeOldResultsAsync(stoppingToken);
                    lastPurgeTime = DateTime.UtcNow;
                }

                // Dequeue bloqueante
                var jobRequest = await _queue.DequeueJobAsync(stoppingToken);
                await ProcessJobAsync(jobRequest, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Unexpected error in ONNX inference background worker loop.");
            }
        }

        _logger.LogInformation("🛑 ONNX Inference Background Worker stopped.");
    }

    internal async Task ReenqueueInterruptedJobsAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AgenticDbContext>();
        var tenantStore = scope.ServiceProvider.GetRequiredService<ITenantStore>();
        var tenantContextAccessor = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
        var systemOperations = scope.ServiceProvider.GetRequiredService<ISystemOperationContextAccessor>();
        IReadOnlyList<AgenticSystem.Core.Models.Tenant> tenants;
        using (systemOperations.BeginScope(AgenticSystem.Core.Models.SystemOperationKind.ProcessOnnxJobs))
        {
            systemOperations.Require(AgenticSystem.Core.Models.SystemOperationKind.ProcessOnnxJobs);
            tenants = await tenantStore.GetAllAsync(ct);
        }

        foreach (var tenant in tenants)
        {
            using var tenantScope = tenantContextAccessor.BeginScope(new AgenticSystem.Core.Models.TenantContext
            {
                TenantId = tenant.Id,
                IsAuthenticated = true
            });

            var interruptedJobs = await dbContext.CustomOnnxInferenceJobs
                .Where(job => job.Status == "Pending" || job.Status == "Processing")
                .OrderBy(job => job.CreatedAt)
                .ToListAsync(ct);
            if (interruptedJobs.Count == 0) continue;

            _logger.LogInformation("🔄 Found {Count} interrupted/pending ONNX inference jobs for tenant {TenantId}. Re-enqueuing...", interruptedJobs.Count, tenant.Id);
            foreach (var job in interruptedJobs)
            {
                if (job.Status == "Processing")
                {
                    job.Status = "Pending";
                    dbContext.Entry(job).State = EntityState.Modified;
                }

                await _queue.EnqueueJobAsync(new OnnxInferenceJobRequest(
                    job.Id,
                    tenant.Id,
                    job.ModelId,
                    job.InputImagePath ?? string.Empty
                ), ct);
            }

            await dbContext.SaveChangesAsync(ct);
        }

        _logger.LogInformation("✅ Interrupted ONNX inference jobs successfully re-enqueued by tenant.");
    }

    internal async Task ProcessJobAsync(OnnxInferenceJobRequest request, CancellationToken ct)
    {
        _logger.LogInformation("Processing ONNX inference job {JobId} for Tenant {TenantId} using Model {ModelId}", request.JobId, request.TenantId, request.ModelId);

        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AgenticDbContext>();
        var toolManager = scope.ServiceProvider.GetRequiredService<IToolManager>();
        var tenantContextAccessor = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();

        if (AgenticSystem.Core.Models.TenantIdPolicy.IsReservedSystemId(request.TenantId))
        {
            _logger.LogError("ONNX job {JobId} carries a reserved synthetic tenant ID; refusing to process it.", request.JobId);
            return;
        }

        using var tenantScope = tenantContextAccessor.BeginScope(new AgenticSystem.Core.Models.TenantContext
        {
            TenantId = request.TenantId,
            IsAuthenticated = true
        });

        var jobEntity = await dbContext.CustomOnnxInferenceJobs
            .FirstOrDefaultAsync(j => j.Id == request.JobId && j.TenantId == request.TenantId, ct);

        if (jobEntity == null)
        {
            _logger.LogWarning("⚠️ ONNX inference job {JobId} was not found in DB. Skipping.", request.JobId);
            return;
        }

        try
        {
            var model = await dbContext.CustomOnnxModels
                .FirstOrDefaultAsync(m => m.Id == request.ModelId && m.TenantId == request.TenantId, ct)
                ?? throw new InvalidOperationException("ONNX model not found.");
            _inputLimits.Validate(model.InputWidth, model.InputHeight, model.Channels);

            // Atualiza status para Processing
            jobEntity.Status = "Processing";
            await dbContext.SaveChangesAsync(ct);

            // Transmite status de processamento via broadcaster
            await _broadcaster.BroadcastJobStatusAsync(request.TenantId, request.JobId, "Processing");

            // Carrega imagem de entrada física e codifica em Base64
            var webRoot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var relativeInputPath = request.InputImagePath.TrimStart('/');
            var absoluteInputPath = Path.Combine(webRoot, relativeInputPath);

            if (!File.Exists(absoluteInputPath))
            {
                throw new FileNotFoundException($"Input image not found at absolute path: {absoluteInputPath}");
            }

            var inputImageBytes = await File.ReadAllBytesAsync(absoluteInputPath, ct);
            var base64Input = Convert.ToBase64String(inputImageBytes);

            // Configura a entrada do DynamicOnnxProcessorTool
            var toolInput = new ToolInput
            {
                Action = "process",
                Parameters = new Dictionary<string, object>
                {
                    { "modelId", request.ModelId },
                    { "imageData", base64Input }
                },
                UserId = "background-worker"
            };

            // Executa a inferência usando o tool manager do MAF
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var result = await toolManager.ExecuteToolAsync("onnx_processor", toolInput, ct);
            stopwatch.Stop();

            if (!result.Success)
            {
                throw new Exception(result.ErrorMessage ?? "ONNX model execution failed without detailed message.");
            }

            // Extrai a imagem base64 de saída e latência retornada pelo Tool
            string? outputImageBase64 = null;
            long latencyMs = stopwatch.ElapsedMilliseconds;

            if (result.Data != null)
            {
                var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(JsonSerializer.Serialize(result.Data));
                if (dict != null)
                {
                    if (dict.TryGetValue("outputImage", out var outImgObj))
                    {
                        outputImageBase64 = outImgObj?.ToString();
                    }
                    if (dict.TryGetValue("latencyMs", out var latObj) && latObj != null)
                    {
                        if (long.TryParse(latObj.ToString(), out var parsedLatency))
                        {
                            latencyMs = parsedLatency;
                        }
                    }
                }
            }

            if (string.IsNullOrEmpty(outputImageBase64))
            {
                throw new Exception("ONNX tool executed successfully but returned empty outputImage.");
            }

            // Grava a imagem física de saída em disco
            var outputImageBytes = Convert.FromBase64String(outputImageBase64);
            var tenantResultsDir = Path.Combine(webRoot, "onnx-results", request.TenantId);
            Directory.CreateDirectory(tenantResultsDir);

            var relativeOutputPath = $"/onnx-results/{request.TenantId}/{request.JobId}_output.png";
            var absoluteOutputPath = Path.Combine(webRoot, relativeOutputPath.TrimStart('/'));

            await File.WriteAllBytesAsync(absoluteOutputPath, outputImageBytes, ct);

            // Finaliza o job com status Completed no banco de dados
            jobEntity.Status = "Completed";
            jobEntity.OutputImagePath = relativeOutputPath;
            jobEntity.LatencyMs = latencyMs;
            jobEntity.CompletedAt = DateTime.UtcNow;

            await dbContext.SaveChangesAsync(ct);

            // Transmite conclusão com sucesso
            await _broadcaster.BroadcastJobStatusAsync(
                request.TenantId,
                request.JobId,
                "Completed",
                latencyMs: latencyMs,
                outputImagePath: relativeOutputPath
            );

            _logger.LogInformation("✅ ONNX background inference job {JobId} completed in {Latency}ms.", request.JobId, latencyMs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ ONNX background inference job {JobId} failed.", request.JobId);

            // Atualiza status para Failed no DB
            jobEntity.Status = "Failed";
            jobEntity.ErrorMessage = ex.Message;
            jobEntity.CompletedAt = DateTime.UtcNow;

            try
            {
                await dbContext.SaveChangesAsync(ct);
            }
            catch (Exception dbEx)
            {
                _logger.LogError(dbEx, "❌ Failed to save failure state in database for job {JobId}", request.JobId);
            }

            // Transmite falha
            await _broadcaster.BroadcastJobStatusAsync(
                request.TenantId,
                request.JobId,
                "Failed",
                error: ex.Message
            );
        }
    }

    private async Task PurgeOldResultsAsync(CancellationToken ct)
    {
        try
        {
            var webRoot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var resultsDir = Path.Combine(webRoot, "onnx-results");
            if (!Directory.Exists(resultsDir)) return;

            var cutoffTime = DateTime.UtcNow.AddDays(-7);
            _logger.LogInformation("🧹 Starting physical purge of ONNX results older than 7 days...");

            // Varre arquivos .png
            var files = Directory.GetFiles(resultsDir, "*.png", SearchOption.AllDirectories);
            int deletedFilesCount = 0;
            using (var filesystemScope = _serviceProvider.CreateScope())
            {
                var systemOperations = filesystemScope.ServiceProvider.GetRequiredService<ISystemOperationContextAccessor>();
                using (systemOperations.BeginScope(AgenticSystem.Core.Models.SystemOperationKind.ProcessOnnxJobs))
                {
                    systemOperations.Require(AgenticSystem.Core.Models.SystemOperationKind.ProcessOnnxJobs);
                    foreach (var file in files)
                    {
                        var fileInfo = new FileInfo(file);
                        if (fileInfo.LastWriteTimeUtc < cutoffTime)
                        {
                            File.Delete(file);
                            deletedFilesCount++;
                        }
                    }
                }
            }

            // Exclui do banco de dados registros criados há mais de 7 dias
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AgenticDbContext>();
            var tenantStore = scope.ServiceProvider.GetRequiredService<ITenantStore>();
            var tenantContextAccessor = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
            var tenants = await tenantStore.GetAllAsync(ct);
            var deletedJobsCount = 0;
            foreach (var tenant in tenants)
            {
                using var tenantScope = tenantContextAccessor.BeginScope(new AgenticSystem.Core.Models.TenantContext
                {
                    TenantId = tenant.Id,
                    IsAuthenticated = true
                });
                var oldJobs = await dbContext.CustomOnnxInferenceJobs
                    .Where(job => job.CreatedAt < cutoffTime)
                    .ToListAsync(ct);
                if (oldJobs.Count == 0) continue;

                dbContext.CustomOnnxInferenceJobs.RemoveRange(oldJobs);
                deletedJobsCount += oldJobs.Count;
                await dbContext.SaveChangesAsync(ct);
            }

            if (deletedJobsCount > 0)
                _logger.LogInformation("🧹 Physical purge complete. Deleted {DeletedFiles} files and {DeletedJobs} DB records.", deletedFilesCount, deletedJobsCount);
            else if (deletedFilesCount > 0)
            {
                _logger.LogInformation("🧹 Physical purge complete. Deleted {DeletedFiles} files (0 records found in DB).", deletedFilesCount);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error during physical purge of old ONNX result files");
        }
    }
}
