using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgenticSystem.Infrastructure.Memory;

/// <summary>
/// A proxy for IVectorStore that supports hot-swapping implementations at runtime without application restart.
/// Part of the "Hot-Swapping Foundation" (Phase 0).
/// </summary>
public sealed class HotSwappableVectorStore : IVectorStore, IDisposable
{
    private readonly IVectorStoreFactory _factory;
    private readonly IOptionsMonitor<MemorySettings> _optionsMonitor;
    private readonly ILogger<HotSwappableVectorStore> _logger;
    private readonly IDisposable? _onChangeSubscription;
    
    private volatile IVectorStore? _currentStore;
    private readonly object _lock = new();
    private bool _disposed;

    public HotSwappableVectorStore(
        IVectorStoreFactory factory,
        IOptionsMonitor<MemorySettings> optionsMonitor,
        ILogger<HotSwappableVectorStore> logger)
    {
        _factory = factory;
        _optionsMonitor = optionsMonitor;
        _logger = logger;
        
        // Listen for configuration changes to invalidate the current store instance
        _onChangeSubscription = _optionsMonitor.OnChange(settings =>
        {
            _logger.LogInformation("🔄 Hot-swapping Vector Store implementation due to configuration change...");
            lock (_lock)
            {
                if (_currentStore is IDisposable disposable)
                {
                    try { disposable.Dispose(); } catch (Exception ex) { _logger.LogWarning(ex, "Failed to dispose old Vector Store instance."); }
                }
                _currentStore = null; // Forces re-creation on next access
            }
        });
    }

    private IVectorStore GetStore()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_currentStore != null) return _currentStore;

        lock (_lock)
        {
            if (_currentStore == null)
            {
                _logger.LogDebug("🏗️ Resolving current Vector Store implementation via factory...");
                _currentStore = _factory.Create();
            }
            return _currentStore;
        }
    }

    public Task UpsertAsync(EmbeddingDocument document) => GetStore().UpsertAsync(document);

    public Task DeleteAsync(string id, string? collection = null) => GetStore().DeleteAsync(id, collection);

    public Task<SearchResult> SearchAsync(string query, SearchScope scope = SearchScope.All, int maxResults = 10) 
        => GetStore().SearchAsync(query, scope, maxResults);

    public Task<SearchResult> SearchWithFiltersAsync(string query, Dictionary<string, string> filters) 
        => GetStore().SearchWithFiltersAsync(query, filters);

    public Task<IEnumerable<string>> GetCollectionsAsync() => GetStore().GetCollectionsAsync();

    public Task CleanupOldDocumentsAsync(TimeSpan olderThan) => GetStore().CleanupOldDocumentsAsync(olderThan);

    public Task<VectorStoreStats> GetStatsAsync(string tenantId, CancellationToken ct = default) => GetStore().GetStatsAsync(tenantId, ct);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _onChangeSubscription?.Dispose();
        if (_currentStore is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}

