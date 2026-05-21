using System;
using System.Collections.Concurrent;
using System.Threading;
using Microsoft.ML.OnnxRuntime;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Core.Services.Ml;

public interface IOnnxSessionCache : IDisposable
{
    InferenceSession GetOrCreateSession(string modelId, string? modelPath, byte[]? modelBytes);
}

public sealed class OnnxSessionCache : IOnnxSessionCache
{
    private sealed class CacheEntry : IDisposable
    {
        public InferenceSession Session { get; }
        public DateTime LastAccessed { get; set; }

        public CacheEntry(InferenceSession session)
        {
            Session = session;
            LastAccessed = DateTime.UtcNow;
        }

        public void Dispose()
        {
            Session.Dispose();
        }
    }

    private readonly ConcurrentDictionary<string, Lazy<CacheEntry>> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger<OnnxSessionCache> _logger;
    private readonly Timer _cleanupTimer;
    private readonly TimeSpan _unusedTimeout = TimeSpan.FromMinutes(15);
    private int _disposed;

    public OnnxSessionCache(ILogger<OnnxSessionCache> logger)
    {
        _logger = logger;
        // Clean up expired sessions every 5 minutes
        _cleanupTimer = new Timer(CleanupExpiredSessions, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
    }

    public InferenceSession GetOrCreateSession(string modelId, string? modelPath, byte[]? modelBytes)
    {
        if (_disposed == 1)
        {
            throw new ObjectDisposedException(nameof(OnnxSessionCache));
        }

        var lazyEntry = _cache.GetOrAdd(modelId, id => new Lazy<CacheEntry>(() =>
        {
            _logger.LogInformation("Initializing new InferenceSession for ONNX model: {ModelId}", id);
            var session = CreateSession(modelPath, modelBytes);
            return new CacheEntry(session);
        }, LazyThreadSafetyMode.ExecutionAndPublication));

        try
        {
            var entry = lazyEntry.Value;
            entry.LastAccessed = DateTime.UtcNow;
            return entry.Session;
        }
        catch (Exception ex)
        {
            // If creation failed, remove the lazy item so future calls can retry
            _cache.TryRemove(modelId, out _);
            _logger.LogError(ex, "Failed to initialize InferenceSession for ONNX model: {ModelId}", modelId);
            throw;
        }
    }

    private static InferenceSession CreateSession(string? modelPath, byte[]? modelBytes)
    {
        try
        {
            return !string.IsNullOrEmpty(modelPath) && System.IO.File.Exists(modelPath)
                ? new InferenceSession(modelPath)
                : new InferenceSession(modelBytes ?? throw new InvalidOperationException("Model data not available."));
        }
        catch (OnnxRuntimeException ex) when (ex.Message.Contains("two nodes with same node name") || ex.Message.Contains("invalid model") || ex.Message.Contains("ErrorCode:Fail"))
        {
            var fallbackOptions = new SessionOptions
            {
                GraphOptimizationLevel = GraphOptimizationLevel.ORT_DISABLE_ALL
            };
            return !string.IsNullOrEmpty(modelPath) && System.IO.File.Exists(modelPath)
                ? new InferenceSession(modelPath, fallbackOptions)
                : new InferenceSession(modelBytes!, fallbackOptions);
        }
    }

    private void CleanupExpiredSessions(object? state)
    {
        try
        {
            var now = DateTime.UtcNow;
            foreach (var kv in _cache)
            {
                if (kv.Value.IsValueCreated)
                {
                    var entry = kv.Value.Value;
                    if (now - entry.LastAccessed > _unusedTimeout)
                    {
                        if (_cache.TryRemove(kv.Key, out var lazyRemoved) && lazyRemoved.IsValueCreated)
                        {
                            _logger.LogInformation("Evicting and disposing idle ONNX InferenceSession for model: {ModelId}", kv.Key);
                            lazyRemoved.Value.Dispose();
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during ONNX session cache cleanup");
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _cleanupTimer.Dispose();
            foreach (var kv in _cache)
            {
                if (kv.Value.IsValueCreated)
                {
                    kv.Value.Value.Dispose();
                }
            }
            _cache.Clear();
            _logger.LogInformation("ONNX session cache disposed.");
        }
    }
}
