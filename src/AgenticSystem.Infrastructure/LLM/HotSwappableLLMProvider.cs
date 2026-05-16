using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.LLM.Interfaces;
using AgenticSystem.Core.LLM.Models;
using AgenticSystem.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgenticSystem.Infrastructure.LLM;

/// <summary>
/// A proxy for ILLMProvider that supports hot-swapping implementations at runtime without application restart.
/// Uses IOptionsMonitor to detect configuration changes and invalidate the cached provider, 
/// avoiding unnecessary re-creation on every call.
/// Part of the "Hot-Swapping Foundation" (Phase 0).
/// </summary>
public sealed class HotSwappableLLMProvider : ILLMProvider, IDisposable
{
    private readonly ILLMProviderFactory _factory;
    private readonly IOptionsMonitor<AgenticSystemSettings> _optionsMonitor;
    private readonly ILogger<HotSwappableLLMProvider> _logger;
    private readonly IDisposable? _onChangeSubscription;

    private volatile ILLMProvider? _currentProvider;
    private readonly object _lock = new();
    private bool _disposed;

    public HotSwappableLLMProvider(
        ILLMProviderFactory factory,
        IOptionsMonitor<AgenticSystemSettings> optionsMonitor,
        ILogger<HotSwappableLLMProvider> logger)
    {
        _factory = factory;
        _optionsMonitor = optionsMonitor;
        _logger = logger;

        _onChangeSubscription = _optionsMonitor.OnChange(settings =>
        {
            _logger.LogInformation("🔄 Hot-swapping LLM Provider due to configuration change...");
            lock (_lock)
            {
                if (_currentProvider is IDisposable disposable)
                {
                    try { disposable.Dispose(); } catch (Exception ex) { _logger.LogWarning(ex, "Failed to dispose old LLM Provider instance."); }
                }
                _currentProvider = null;
            }
        });
    }

    public string Name => GetProvider().Name;

    public string DefaultModel => GetProvider().DefaultModel;

    public bool IsEnabled => GetProvider().IsEnabled;

    public int Priority => GetProvider().Priority;

    public Task<LLMResponse> GenerateAsync(LLMRequest request, CancellationToken ct = default)
    {
        return GetProvider().GenerateAsync(request, ct);
    }

    public Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        return GetProvider().IsAvailableAsync(ct);
    }

    public void Configure(string? apiKey, string? defaultModel, bool? enabled, int? priority)
    {
        GetProvider().Configure(apiKey, defaultModel, enabled, priority);
    }

    private ILLMProvider GetProvider()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_currentProvider != null) return _currentProvider;

        lock (_lock)
        {
            if (_currentProvider == null)
            {
                _logger.LogDebug("🏗️ Resolving current LLM Provider implementation via factory...");
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
