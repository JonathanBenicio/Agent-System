using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using AgenticSystem.Core.LLM.Interfaces;

namespace AgenticSystem.Infrastructure.Embeddings;

/// <summary>
/// Adapter que wrapa um IEmbeddingGenerator&lt;string, Embedding&lt;float&gt;&gt; (M.E.AI)
/// como IEmbeddingProvider. Permite usar qualquer provider M.E.AI no pipeline existente.
/// </summary>
public class EmbeddingProviderAdapter : IEmbeddingProvider
{
    private readonly IEmbeddingGenerator<string, Embedding<float>> _generator;
    private readonly ILogger<EmbeddingProviderAdapter> _logger;

    public EmbeddingProviderAdapter(
        IEmbeddingGenerator<string, Embedding<float>> generator,
        ILogger<EmbeddingProviderAdapter> logger,
        string name = "M.E.AI",
        string defaultModel = "text-embedding-3-small",
        int dimensions = 1536,
        int priority = -1)
    {
        _generator = generator;
        _logger = logger;
        Name = name;
        DefaultModel = defaultModel;
        Dimensions = dimensions;
        Priority = priority;
    }

    public string Name { get; }
    public string DefaultModel { get; }
    public int Dimensions { get; }
    public bool IsEnabled => true;
    public int Priority { get; }

    public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken ct = default)
    {
        var result = await _generator.GenerateAsync(text, cancellationToken: ct);
        _logger.LogDebug("📐 M.E.AI Embedding: 1 vector, {Dims} dimensions", result.Vector.Length);
        return result.Vector.ToArray();
    }

    public async Task<IReadOnlyList<float[]>> GenerateEmbeddingsAsync(IEnumerable<string> texts, CancellationToken ct = default)
    {
        var textList = texts.ToList();
        var results = await _generator.GenerateAsync(textList, cancellationToken: ct);

        _logger.LogDebug("📐 M.E.AI Embeddings: {Count} vectors", results.Count);

        return results
            .Select(e => e.Vector.ToArray())
            .ToList()
            .AsReadOnly();
    }

    public Task<bool> IsAvailableAsync(CancellationToken ct = default) => Task.FromResult(true);
}
