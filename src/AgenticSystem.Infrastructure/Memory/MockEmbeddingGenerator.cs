using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;

namespace AgenticSystem.Infrastructure.Memory;

/// <summary>
/// Mock implementation of IEmbeddingGenerator for load testing.
/// Returns fixed vectors without calling any external API.
/// </summary>
public sealed class MockEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
{
    private readonly int _dimensions;

    public MockEmbeddingGenerator(int dimensions = 1536)
    {
        _dimensions = dimensions;
    }

    public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var embeddings = values.Select(v => new Embedding<float>(GenerateMockVector(v))).ToList();
        var result = new GeneratedEmbeddings<Embedding<float>>(embeddings);
        return Task.FromResult(result);
    }

    public void Dispose()
    {
    }

    private float[] GenerateMockVector(string text)
    {
        var vector = new float[_dimensions];
        // Fill with a non-zero value to avoid issues with cosine similarity
        Array.Fill(vector, 0.1f);
        return vector;
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        return serviceType.IsInstanceOfType(this) ? this : null;
    }
}
