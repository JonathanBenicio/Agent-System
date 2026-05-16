using AgenticSystem.Core.Interfaces;
using AgenticSystem.Infrastructure.Configuration;
using AgenticSystem.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AgenticSystem.Infrastructure.Memory;

/// <summary>
/// Factory that resolves the concrete IVectorStore implementation based on current configuration.
/// Supporting the "Hot-Swapping Foundation" (Phase 0).
/// </summary>
public sealed class VectorStoreFactory : IVectorStoreFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IOptionsMonitor<MemorySettings> _optionsMonitor;

    public VectorStoreFactory(
        IServiceProvider serviceProvider,
        IOptionsMonitor<MemorySettings> optionsMonitor)
    {
        _serviceProvider = serviceProvider;
        _optionsMonitor = optionsMonitor;
    }

    public IVectorStore Create()
    {
        var settings = _optionsMonitor.CurrentValue;
        
        // Strategy: Based on VectorStoreType, resolve the corresponding concrete implementation.
        // These concrete implementations must be registered as transient or scoped in the container.
        
        return settings.VectorStoreType.ToLowerInvariant() switch
        {
            "postgresql" or "postgres" => ActivatorUtilities.CreateInstance<PostgresVectorStore>(_serviceProvider),
            "sqlite" => ActivatorUtilities.CreateInstance<SqliteVectorStore>(_serviceProvider),
            "qdrant" => ActivatorUtilities.CreateInstance<QdrantVectorStore>(_serviceProvider, _serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient()),
            "pinecone" => ActivatorUtilities.CreateInstance<PineconeVectorStore>(_serviceProvider, _serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient("Pinecone")),
            _ => ActivatorUtilities.CreateInstance<InMemoryVectorStore>(_serviceProvider)
        };
    }
}
