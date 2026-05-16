using AgenticSystem.Core.LLM.Interfaces;

namespace AgenticSystem.Core.Interfaces;

/// <summary>
/// Generic factory for hot-swappable services.
/// </summary>
public interface IHotSwappableFactory<TService> where TService : class
{
    /// <summary>
    /// Creates the current implementation based on the active configuration.
    /// </summary>
    TService Create();
}

/// <summary>
/// Factory specifically for IVectorStore implementations.
/// </summary>
public interface IVectorStoreFactory : IHotSwappableFactory<IVectorStore>
{
}

/// <summary>
/// Factory specifically for ILLMProvider implementations.
/// </summary>
public interface ILLMProviderFactory : IHotSwappableFactory<ILLMProvider>
{
}

/// <summary>
/// Factory specifically for IEmbeddingProvider implementations.
/// Part of the "Hot-Swapping Foundation" (Phase 0) — completes the factory triad.
/// </summary>
public interface IEmbeddingProviderFactory : IHotSwappableFactory<IEmbeddingProvider>
{
}
