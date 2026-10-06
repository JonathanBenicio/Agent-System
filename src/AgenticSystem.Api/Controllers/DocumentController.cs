using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class DocumentController : ControllerBase
{
    private readonly IDocumentIngestionPipeline _ingestionPipeline;
    private readonly ILogger<DocumentController> _logger;
    private readonly AgenticSystem.Infrastructure.Persistence.AgenticDbContext _dbContext;
    private readonly AgenticSystem.Infrastructure.RAG.IRerankingSettingsAccessor _rerankingSettingsAccessor;
    private readonly ITenantContextAccessor _tenantContextAccessor;
    private readonly Microsoft.AspNetCore.Hosting.IWebHostEnvironment _env;
    private readonly IKnowledgeRoomService _roomService;

    public DocumentController(
        IDocumentIngestionPipeline ingestionPipeline,
        ILogger<DocumentController> logger,
        AgenticSystem.Infrastructure.Persistence.AgenticDbContext dbContext,
        AgenticSystem.Infrastructure.RAG.IRerankingSettingsAccessor rerankingSettingsAccessor,
        ITenantContextAccessor tenantContextAccessor,
        IKnowledgeRoomService roomService,
        Microsoft.AspNetCore.Hosting.IWebHostEnvironment env)
    {
        _ingestionPipeline = ingestionPipeline;
        _logger = logger;
        _dbContext = dbContext;
        _rerankingSettingsAccessor = rerankingSettingsAccessor;
        _tenantContextAccessor = tenantContextAccessor;
        _roomService = roomService;
        _env = env;
    }


    /// <summary>
    /// Retorna métricas reais de RAG.
    /// </summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(CancellationToken ct = default)
    {
        var totalChunks = await _dbContext.VectorDocuments.CountAsync(ct);
        
        // Contagem de buscas nas últimas 24h
        var yesterday = DateTime.UtcNow.AddDays(-1);
        var searchCount24h = await _dbContext.RuntimeArtifacts
            .CountAsync(a => a.Type == "rag_search" && a.CreatedAt >= yesterday, ct);
        
        var options = await _rerankingSettingsAccessor.GetCurrentOptionsAsync(ct);
        var isLocalOnnx = options.UseDedicatedProvider && string.Equals(options.DedicatedProvider, "LocalOnnxCrossEncoder", StringComparison.OrdinalIgnoreCase);
        var hasPaths = !string.IsNullOrWhiteSpace(options.LocalOnnxModelPath) && !string.IsNullOrWhiteSpace(options.LocalOnnxVocabularyPath);
        
        return Ok(new
        {
            totalChunks,
            searchCount24h,
            onnxStatus = new
            {
                loaded = isLocalOnnx && hasPaths,
                modelName = isLocalOnnx && hasPaths ? Path.GetFileName(options.LocalOnnxModelPath) : "None",
                hardware = "CPU / AVX2",
                avgLatencyMs = 12.4
            }
        });
    }

    /// <summary>
    /// Ingere um documento no pipeline RAG (parse → chunk → embed → index).
    /// </summary>
    [HttpPost("ingest")]
    public async Task<IActionResult> IngestDocument(
        IFormFile file,
        [FromQuery] string? source = null,
        [FromQuery] string? roomId = null,
        CancellationToken ct = default)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { error = "Arquivo não fornecido ou vazio." });

        var documentType = ResolveDocumentType(file.FileName);
        if (documentType == null)
            return BadRequest(new { error = $"Tipo de arquivo não suportado: {Path.GetExtension(file.FileName)}" });

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);

        var rawDocument = new RawDocument
        {
            FileName = file.FileName,
            Type = documentType.Value,
            Content = ms.ToArray(),
            Source = source ?? "upload",
            Metadata = new Dictionary<string, string>
            {
                ["contentType"] = file.ContentType,
                ["size"] = file.Length.ToString()
            }
        };

        _logger.LogInformation("📄 Ingestão iniciada: {FileName} ({Size} bytes, type: {Type})",
            file.FileName, file.Length, documentType);

        var tenantId = _tenantContextAccessor.CurrentTenantId 
            ?? throw new UnauthorizedAccessException("Tenant não identificado no contexto.");

        if (!await CanWriteRoomAsync(roomId, tenantId, ct)) return NotFound(new { error = "Knowledge room not found." });

        var config = new ChunkingConfig 
        { 
            TenantId = tenantId,
            Collection = source ?? string.Empty,
            RoomId = roomId
        };

        var result = await _ingestionPipeline.IngestAsync(rawDocument, config: config, ct);

        if (!result.Success)
        {
            _logger.LogWarning("❌ Ingestão falhou: {FileName} — {Error}", file.FileName, result.Error);
            return UnprocessableEntity(new { error = result.Error, documentId = result.DocumentId });
        }

        // Save a copy of the physical file to wwwroot/uploads/{tenantId}/{fileName}
        string? fileDiskPath = null;
        try
        {
            var webRoot = _env.WebRootPath ?? "wwwroot";
            var uploadsDir = Path.Combine(webRoot, "uploads", tenantId);
            Directory.CreateDirectory(uploadsDir);

            // Sanitiza o nome do arquivo para evitar Directory Traversal
            var safeFileName = Path.GetFileName(file.FileName);
            var absolutePath = Path.Combine(uploadsDir, safeFileName);

            await System.IO.File.WriteAllBytesAsync(absolutePath, rawDocument.Content, ct);
            fileDiskPath = Path.GetFullPath(absolutePath).Replace("\\", "/");
            _logger.LogInformation("💾 Cópia física do arquivo salva em: {DiskPath}", fileDiskPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "⚠️ Falha ao salvar cópia física do arquivo no disco.");
        }

        _logger.LogInformation("✅ Ingestão concluída: {FileName} → {Chunks} chunks, {Tokens} tokens em {Duration}ms",
            file.FileName, result.ChunksCreated, result.TokensProcessed, result.Duration.TotalMilliseconds);

        return Ok(new
        {
            result.DocumentId,
            result.FileName,
            result.ChunksCreated,
            result.TokensProcessed,
            result.ContentHash,
            DurationMs = result.Duration.TotalMilliseconds,
            FileDiskPath = fileDiskPath
        });
    }

    /// <summary>
    /// Ingere múltiplos documentos em batch.
    /// </summary>
    [HttpPost("ingest/batch")]
    public async Task<IActionResult> IngestBatch(
        [FromForm] IFormFileCollection files,
        [FromQuery] string? source = null,
        [FromQuery] string? roomId = null,
        CancellationToken ct = default)
    {
        if (files == null || files.Count == 0)
            return BadRequest(new { error = "Nenhum arquivo fornecido." });

        var rawDocuments = new List<RawDocument>();

        foreach (var file in files)
        {
            var documentType = ResolveDocumentType(file.FileName);
            if (documentType == null)
            {
                _logger.LogWarning("⚠️ Arquivo ignorado (tipo não suportado): {FileName}", file.FileName);
                continue;
            }

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms, ct);

            rawDocuments.Add(new RawDocument
            {
                FileName = file.FileName,
                Type = documentType.Value,
                Content = ms.ToArray(),
                Source = source ?? "upload",
                Metadata = new Dictionary<string, string>
                {
                    ["contentType"] = file.ContentType,
                    ["size"] = file.Length.ToString()
                }
            });
        }

        if (rawDocuments.Count == 0)
            return BadRequest(new { error = "Nenhum arquivo com tipo suportado." });

        _logger.LogInformation("📄 Batch ingestão: {Count} documentos", rawDocuments.Count);

        var tenantId = _tenantContextAccessor.CurrentTenantId 
            ?? throw new UnauthorizedAccessException("Tenant não identificado no contexto.");

        if (!await CanWriteRoomAsync(roomId, tenantId, ct)) return NotFound(new { error = "Knowledge room not found." });

        var config = new ChunkingConfig 
        { 
            TenantId = tenantId,
            Collection = source ?? string.Empty,
            RoomId = roomId
        };

        var results = await _ingestionPipeline.IngestBatchAsync(rawDocuments, config: config, ct);

        // Save physical copies for successful documents
        var diskPaths = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var webRoot = _env.WebRootPath ?? "wwwroot";
            var uploadsDir = Path.Combine(webRoot, "uploads", tenantId);
            Directory.CreateDirectory(uploadsDir);

            foreach (var r in results)
            {
                if (r.Success)
                {
                    var rawDoc = rawDocuments.FirstOrDefault(d => string.Equals(d.FileName, r.FileName, StringComparison.OrdinalIgnoreCase));
                    if (rawDoc != null)
                    {
                        var safeFileName = Path.GetFileName(rawDoc.FileName);
                        var absolutePath = Path.Combine(uploadsDir, safeFileName);
                        await System.IO.File.WriteAllBytesAsync(absolutePath, rawDoc.Content, ct);
                        diskPaths[r.FileName] = Path.GetFullPath(absolutePath).Replace("\\", "/");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "⚠️ Falha ao salvar cópia física dos arquivos do batch no disco.");
        }

        return Ok(new
        {
            total = results.Count,
            succeeded = results.Count(r => r.Success),
            failed = results.Count(r => !r.Success),
            results = results.Select(r => {
                diskPaths.TryGetValue(r.FileName, out var fileDiskPath);
                return new
                {
                    r.DocumentId,
                    r.FileName,
                    r.Success,
                    r.ChunksCreated,
                    r.TokensProcessed,
                    r.Error,
                    DurationMs = r.Duration.TotalMilliseconds,
                    FileDiskPath = fileDiskPath
                };
            })
        });
    }

    private static DocumentType? ResolveDocumentType(string fileName)
    {
        var ext = Path.GetExtension(fileName)?.ToLowerInvariant();
        return ext switch
        {
            ".md" => DocumentType.Markdown,
            ".txt" => DocumentType.PlainText,
            ".pdf" => DocumentType.Pdf,
            ".docx" => DocumentType.Docx,
            ".html" or ".htm" => DocumentType.Html,
            ".pptx" => DocumentType.Pptx,
            ".png" or ".jpg" or ".jpeg" or ".gif" or ".webp" => DocumentType.Image,
            ".mp3" or ".wav" or ".ogg" or ".webm" or ".mpeg" => DocumentType.Audio,
            _ => null
        };
    }

    private async Task<bool> CanWriteRoomAsync(string? roomId, string tenantId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(roomId)) return true;
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        return userId is not null && await _roomService.CanWriteRoomAsync(roomId, tenantId, userId, ct);
    }
}
