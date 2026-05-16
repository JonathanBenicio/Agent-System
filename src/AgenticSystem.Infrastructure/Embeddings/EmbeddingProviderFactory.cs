using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.LLM.Interfaces;
using AgenticSystem.Infrastructure.Configuration;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgenticSystem.Infrastructure.Embeddings;

/// <summary>
/// Factory that resolves the concrete IEmbeddingProvider implementation based on current configuration.
/// Part of the "Hot-Swapping Foundation" (Phase 0) — completes the factory triad.
/// </summary>
public sealed class EmbeddingProviderFactory : IEmbeddingProviderFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IOptionsMonitor<AgenticSystemSettings> _optionsMonitor;

    public EmbeddingProviderFactory(
        IServiceProvider serviceProvider,
        IOptionsMonitor<AgenticSystemSettings> optionsMonitor)
    {
        _serviceProvider = serviceProvider;
        _optionsMonitor = optionsMonitor;
    }

    public IEmbeddingProvider Create()
    {
        // The current system uses M.E.AI IEmbeddingGenerator<string, Embedding<float>>
        // registered via AddEmbeddingGenerator(). Wrap it as our domain IEmbeddingProvider.
        var generator = _serviceProvider.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();
        var logger = _serviceProvider.GetRequiredService<ILogger<EmbeddingProviderAdapter>>();
        var settings = _optionsMonitor.CurrentValue;

        return new EmbeddingProviderAdapter(
            generator,
            logger,
            name: $"Ollama/{settings.Ollama.EmbeddingModel}",
            defaultModel: settings.Ollama.EmbeddingModel,
            dimensions: 768, // nomic-embed-text default
            priority: 1);
    }
}
