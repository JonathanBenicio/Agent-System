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
        var options = CreateSessionOptions();
        try
        {
            return !string.IsNullOrEmpty(modelPath) && System.IO.File.Exists(modelPath)
                ? new InferenceSession(modelPath, options)
                : new InferenceSession(modelBytes ?? throw new InvalidOperationException("Model data not available."), options);
        }
        catch (Exception)
        {
            // If session creation with GPU options fails (e.g. library missing or hardware incompatible),
            // fall back graciosamente to standard sequential CPU options.
            var fallbackOptions = new SessionOptions
            {
                GraphOptimizationLevel = GraphOptimizationLevel.ORT_DISABLE_ALL
            };
            return !string.IsNullOrEmpty(modelPath) && System.IO.File.Exists(modelPath)
                ? new InferenceSession(modelPath, fallbackOptions)
                : new InferenceSession(modelBytes ?? throw new InvalidOperationException("Model data not available."), fallbackOptions);
        }
    }

    private static SessionOptions CreateSessionOptions()
    {
        var options = new SessionOptions();

        // 1. Try DirectML (optimal for local acceleration on Windows)
        try
        {
            var dmlMethod = typeof(SessionOptions).GetMethod("AppendExecutionProvider_Dml", new[] { typeof(int) });
            if (dmlMethod != null)
            {
                dmlMethod.Invoke(options, new object[] { 0 });
                return options;
            }
        }
        catch
        {
            // DirectML not supported or failed to bind
        }

        // 2. Try CUDA (optimal for NVIDIA GPUs)
        try
        {
            var cudaMethod = typeof(SessionOptions).GetMethod("AppendExecutionProvider_CUDA", new[] { typeof(int) });
            if (cudaMethod != null)
            {
                cudaMethod.Invoke(options, new object[] { 0 });
                return options;
            }
        }
        catch
        {
            // CUDA not supported or failed to bind
        }

        // 3. Fall back to standard CPU (sequential execution is safer for resource isolation)
        return options;
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
