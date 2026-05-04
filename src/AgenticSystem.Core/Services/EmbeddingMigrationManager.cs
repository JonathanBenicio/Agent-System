using Microsoft.Extensions.Logging;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Services;

/// <summary>
/// ML23 — Orquestrador de migração de embeddings com re-indexação em background.
/// </summary>
public class EmbeddingMigrationManager : IEmbeddingMigrationManager
{
    private readonly IMigrationJobStore _jobStore;
    private readonly IEmbeddingModelStore _modelStore;
    private readonly IVectorStore _vectorStore;
    private readonly IEmbeddingGenerator _embeddingGenerator;
    private readonly ILogger<EmbeddingMigrationManager> _logger;

    public EmbeddingMigrationManager(
        IMigrationJobStore jobStore,
        IEmbeddingModelStore modelStore,
        IVectorStore vectorStore,
        IEmbeddingGenerator embeddingGenerator,
        ILogger<EmbeddingMigrationManager> logger)
    {
        _jobStore = jobStore;
        _modelStore = modelStore;
        _vectorStore = vectorStore;
        _embeddingGenerator = embeddingGenerator;
        _logger = logger;
    }

    public async Task<EmbeddingMigrationJob> StartMigrationAsync(StartMigrationRequest request)
    {
        var sourceModel = await _modelStore.GetAsync(request.SourceModelId)
            ?? throw new KeyNotFoundException($"Source model '{request.SourceModelId}' not found.");
        var targetModel = await _modelStore.GetAsync(request.TargetModelId)
            ?? throw new KeyNotFoundException($"Target model '{request.TargetModelId}' not found.");

        var sourceCollection = request.SourceCollection ?? "default";
        var targetCollection = $"{sourceCollection}_migration_{DateTime.UtcNow:yyyyMMddHHmmss}";

        var job = new EmbeddingMigrationJob
        {
            SourceCollection = sourceCollection,
            TargetCollection = targetCollection,
            SourceModel = sourceModel,
            TargetModel = targetModel,
            Status = MigrationStatus.Pending
        };

        await _jobStore.SaveAsync(job);
        _logger.LogInformation(
            "Migration job {JobId} created: {Source} ({SourceDim}d) → {Target} ({TargetDim}d)",
            job.Id, sourceModel.ModelName, sourceModel.Dimensions, targetModel.ModelName, targetModel.Dimensions);

        // Start processing in background (não bloqueia)
        _ = Task.Run(async () =>
        {
            try
            {
                await ProcessMigrationAsync(job);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error in migration job {JobId}", job.Id);
                job.Status = MigrationStatus.Failed;
                job.ErrorMessage = ex.Message;
                await _jobStore.SaveAsync(job);
            }
        });

        return job;
    }

    public async Task<EmbeddingMigrationJob?> GetJobAsync(string jobId)
    {
        return await _jobStore.GetAsync(jobId);
    }

    public async Task<IEnumerable<EmbeddingMigrationJob>> GetAllJobsAsync()
    {
        return await _jobStore.GetAllAsync();
    }

    public async Task<MigrationStatusSummary> GetStatusAsync(string jobId)
    {
        var job = await _jobStore.GetAsync(jobId)
            ?? throw new KeyNotFoundException($"Job '{jobId}' not found.");

        var elapsed = job.StartedAt.HasValue
            ? DateTime.UtcNow - job.StartedAt.Value
            : (TimeSpan?)null;

        TimeSpan? estimated = null;
        if (job.ProcessedDocuments > 0 && elapsed.HasValue && job.TotalDocuments > job.ProcessedDocuments)
        {
            var rate = elapsed.Value.TotalSeconds / job.ProcessedDocuments;
            var remaining = (job.TotalDocuments - job.ProcessedDocuments) * rate;
            estimated = TimeSpan.FromSeconds(remaining);
        }

        return new MigrationStatusSummary
        {
            JobId = job.Id,
            Status = job.Status,
            ProgressPercentage = job.ProgressPercentage,
            TotalDocuments = job.TotalDocuments,
            ProcessedDocuments = job.ProcessedDocuments,
            FailedDocuments = job.FailedDocuments,
            SourceModel = $"{job.SourceModel.Provider}/{job.SourceModel.ModelName}",
            TargetModel = $"{job.TargetModel.Provider}/{job.TargetModel.ModelName}",
            ElapsedTime = elapsed,
            EstimatedTimeRemaining = estimated
        };
    }

    public async Task CancelAsync(string jobId)
    {
        var job = await _jobStore.GetAsync(jobId)
            ?? throw new KeyNotFoundException($"Job '{jobId}' not found.");

        if (job.Status is MigrationStatus.Completed or MigrationStatus.Cancelled)
            throw new InvalidOperationException($"Cannot cancel job in status '{job.Status}'.");

        job.Status = MigrationStatus.Cancelled;
        job.ProcessingLog.Add($"[{DateTime.UtcNow:O}] Cancelled by user");
        await _jobStore.SaveAsync(job);
        _logger.LogWarning("Migration job {JobId} cancelled", jobId);
    }

    public async Task RetryAsync(string jobId)
    {
        var job = await _jobStore.GetAsync(jobId)
            ?? throw new KeyNotFoundException($"Job '{jobId}' not found.");

        if (job.Status != MigrationStatus.Failed)
            throw new InvalidOperationException("Only failed jobs can be retried.");

        job.Status = MigrationStatus.Pending;
        job.ErrorMessage = null;
        job.ProcessingLog.Add($"[{DateTime.UtcNow:O}] Retry requested");
        await _jobStore.SaveAsync(job);

        _ = Task.Run(async () =>
        {
            try
            {
                await ProcessMigrationAsync(job);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error in retry of migration job {JobId}", job.Id);
                job.Status = MigrationStatus.Failed;
                job.ErrorMessage = ex.Message;
                await _jobStore.SaveAsync(job);
            }
        });
        _logger.LogInformation("Migration job {JobId} retried", jobId);
    }

    public async Task SwitchCollectionAsync(string jobId)
    {
        var job = await _jobStore.GetAsync(jobId)
            ?? throw new KeyNotFoundException($"Job '{jobId}' not found.");

        if (job.Status != MigrationStatus.Completed)
            throw new InvalidOperationException("Can only switch collections for completed jobs.");

        await _modelStore.SetActiveAsync(job.TargetModel.Id);
        job.ProcessingLog.Add($"[{DateTime.UtcNow:O}] Collection switched to target: {job.TargetCollection}");
        await _jobStore.SaveAsync(job);

        _logger.LogInformation("Active model switched to {Model} for job {JobId}",
            job.TargetModel.ModelName, jobId);
    }

    private async Task ProcessMigrationAsync(EmbeddingMigrationJob job)
    {
        try
        {
            job.Status = MigrationStatus.InProgress;
            job.StartedAt = DateTime.UtcNow;
            job.ProcessingLog.Add($"[{DateTime.UtcNow:O}] Migration started");
            await _jobStore.SaveAsync(job);

            // Buscar documentos da coleção source via VectorStore
            var collections = await _vectorStore.GetCollectionsAsync();
            var sourceExists = collections.Contains(job.SourceCollection);

            if (!sourceExists)
            {
                // Se coleção não existe, simular com documentos do store
                job.TotalDocuments = 0;
                job.ProcessingLog.Add($"[{DateTime.UtcNow:O}] Source collection '{job.SourceCollection}' is empty or not found");
            }

            // Buscar todos os docs da coleção para re-vetorizar
            var searchResult = await _vectorStore.SearchAsync("*", SearchScope.All, 10000);
            var documents = searchResult.Matches;
            job.TotalDocuments = documents.Count;
            await _jobStore.SaveAsync(job);

            // Re-indexar em batches de 10
            const int batchSize = 10;
            for (var i = 0; i < documents.Count; i += batchSize)
            {
                if (job.Status == MigrationStatus.Cancelled)
                {
                    _logger.LogInformation("Job {JobId} cancelled during processing", job.Id);
                    return;
                }

                var batch = documents.Skip(i).Take(batchSize).ToList();
                var texts = batch.Select(d => d.Content).ToList();

                try
                {
                    var embeddings = await _embeddingGenerator.GenerateBatchAsync(texts, job.TargetModel);
                    var embeddingList = embeddings.ToList();

                    for (var j = 0; j < batch.Count && j < embeddingList.Count; j++)
                    {
                        var doc = new EmbeddingDocument
                        {
                            Id = batch[j].Id,
                            Content = batch[j].Content,
                            Type = batch[j].Type,
                            Collection = job.TargetCollection,
                            Embedding = embeddingList[j],
                            Metadata = batch[j].Metadata ?? new Dictionary<string, string>()
                        };
                        await _vectorStore.UpsertAsync(doc);
                    }

                    job.ProcessedDocuments += batch.Count;
                }
                catch (Exception ex)
                {
                    job.FailedDocuments += batch.Count;
                    job.ProcessingLog.Add($"[{DateTime.UtcNow:O}] Batch failed at offset {i}: {ex.Message}");
                    _logger.LogWarning(ex, "Batch failed at offset {Offset} for job {JobId}", i, job.Id);
                }

                await _jobStore.SaveAsync(job);
            }

            job.Status = MigrationStatus.Completed;
            job.CompletedAt = DateTime.UtcNow;
            job.ProcessingLog.Add($"[{DateTime.UtcNow:O}] Migration completed: {job.ProcessedDocuments}/{job.TotalDocuments} processed");
            await _jobStore.SaveAsync(job);

            _logger.LogInformation(
                "Migration job {JobId} completed: {Processed}/{Total} docs, {Failed} failed",
                job.Id, job.ProcessedDocuments, job.TotalDocuments, job.FailedDocuments);
        }
        catch (Exception ex)
        {
            job.Status = MigrationStatus.Failed;
            job.ErrorMessage = ex.Message;
            job.ProcessingLog.Add($"[{DateTime.UtcNow:O}] Migration failed: {ex.Message}");
            await _jobStore.SaveAsync(job);
            _logger.LogError(ex, "Migration job {JobId} failed", job.Id);
        }
    }
}
