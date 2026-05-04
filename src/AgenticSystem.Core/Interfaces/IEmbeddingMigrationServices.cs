using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Interfaces;

/// <summary>
/// ML23 — Orquestrador de migração de embeddings.
/// </summary>
public interface IEmbeddingMigrationManager
{
    Task<EmbeddingMigrationJob> StartMigrationAsync(StartMigrationRequest request);
    Task<EmbeddingMigrationJob?> GetJobAsync(string jobId);
    Task<IEnumerable<EmbeddingMigrationJob>> GetAllJobsAsync();
    Task<MigrationStatusSummary> GetStatusAsync(string jobId);
    Task CancelAsync(string jobId);
    Task RetryAsync(string jobId);
    Task SwitchCollectionAsync(string jobId);
}

/// <summary>
/// Store de modelos de embedding configurados.
/// </summary>
public interface IEmbeddingModelStore
{
    Task<EmbeddingModelConfig?> GetAsync(string modelId);
    Task<IEnumerable<EmbeddingModelConfig>> GetAllAsync();
    Task<EmbeddingModelConfig> GetActiveAsync();
    Task SaveAsync(EmbeddingModelConfig model);
    Task DeleteAsync(string modelId);
    Task SetActiveAsync(string modelId);
}

/// <summary>
/// Serviço de geração de embeddings (abstração multi-provider).
/// </summary>
public interface IEmbeddingGenerator
{
    Task<float[]> GenerateAsync(string text, EmbeddingModelConfig model);
    Task<IEnumerable<float[]>> GenerateBatchAsync(IEnumerable<string> texts, EmbeddingModelConfig model);
}

/// <summary>
/// Store de jobs de migração.
/// </summary>
public interface IMigrationJobStore
{
    Task<EmbeddingMigrationJob?> GetAsync(string jobId);
    Task<IEnumerable<EmbeddingMigrationJob>> GetAllAsync();
    Task SaveAsync(EmbeddingMigrationJob job);
    Task DeleteAsync(string jobId);
}
