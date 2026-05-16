using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.LLM.Interfaces;
using AgenticSystem.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgenticSystem.Infrastructure.Embeddings;

/// <summary>
/// A proxy for IEmbeddingProvider that supports hot-swapping implementations at runtime.
/// Uses IOptionsMonitor to detect configuration changes and invalidate the cached provider.
/// Part of the "Hot-Swapping Foundation" (Phase 0).
/// </summary>
public sealed class HotSwappableEmbeddingProvider : IEmbeddingProvider, IDisposable
{
    private readonly IEmbeddingProviderFactory _factory;
    private readonly IOptionsMonitor<AgenticSystemSettings> _optionsMonitor;
    private readonly ILogger<HotSwappableEmbeddingProvider> _logger;
    private readonly IDisposable? _onChangeSubscription;

    private volatile IEmbeddingProvider? _currentProvider;
    private readonly object _lock = new();
    private bool _disposed;

    public HotSwappableEmbeddingProvider(
        IEmbeddingProviderFactory factory,
        IOptionsMonitor<AgenticSystemSettings> optionsMonitor,
        ILogger<HotSwappableEmbeddingProvider> logger)
    {
        _factory = factory;
        _optionsMonitor = optionsMonitor;
        _logger = logger;

        _onChangeSubscription = _optionsMonitor.OnChange(_ =>
        {
            _logger.LogInformation("🔄 Hot-swapping Embedding Provider due to configuration change...");
            lock (_lock)
            {
                if (_currentProvider is IDisposable disposable)
                {
                    try { disposable.Dispose(); } catch (Exception ex) { _logger.LogWarning(ex, "Failed to dispose old Embedding Provider instance."); }
                }
                _currentProvider = null;
            }
        });
    }

    public string Name => GetProvider().Name;
    public string DefaultModel => GetProvider().DefaultModel;
    public int Dimensions => GetProvider().Dimensions;
    public bool IsEnabled => GetProvider().IsEnabled;
    public int Priority => GetProvider().Priority;

    public Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken ct = default)
        => GetProvider().GenerateEmbeddingAsync(text, ct);

    public Task<IReadOnlyList<float[]>> GenerateEmbeddingsAsync(IEnumerable<string> texts, CancellationToken ct = default)
        => GetProvider().GenerateEmbeddingsAsync(texts, ct);

    public Task<bool> IsAvailableAsync(CancellationToken ct = default)
        => GetProvider().IsAvailableAsync(ct);

    private IEmbeddingProvider GetProvider()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_currentProvider != null) return _currentProvider;

        lock (_lock)
        {
            if (_currentProvider == null)
            {
                _logger.LogDebug("🏗️ Resolving current Embedding Provider implementation via factory...");
                _currentProvider = _factory.Create();
            }
            return _currentProvider;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _onChangeSubscription?.Dispose();
        if (_currentProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
