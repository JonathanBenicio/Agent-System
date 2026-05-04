namespace AgenticSystem.Core.LLM.Interfaces;

/// <summary>
/// Provider de embeddings para busca semântica (RAG).
/// </summary>
public interface IEmbeddingProvider
{
    string Name { get; }
    string DefaultModel { get; }
    int Dimensions { get; }
    bool IsEnabled { get; }
    int Priority { get; }

    Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken ct = default);
    Task<IReadOnlyList<float[]>> GenerateEmbeddingsAsync(IEnumerable<string> texts, CancellationToken ct = default);
    Task<bool> IsAvailableAsync(CancellationToken ct = default);
}
