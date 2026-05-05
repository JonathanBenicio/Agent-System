using System.Collections.Concurrent;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Infrastructure.Gateway;

/// <summary>
/// Circuit Breaker sem dependência de Polly.
/// Estados: Closed (normal) → Open (bloqueando) → HalfOpen (testando).
/// </summary>
public class CircuitBreaker
{
    private readonly CircuitBreakerConfig _config;
    private readonly ConcurrentQueue<DateTime> _failures = new();
    private CircuitState _state = CircuitState.Closed;
    private DateTime _openedAt = DateTime.MinValue;
    private int _consecutiveOpens;
    private readonly object _lock = new();

    public CircuitBreaker(CircuitBreakerConfig config)
    {
        _config = config;
    }

    public CircuitState State
    {
        get
        {
            lock (_lock)
            {
                if (_state == CircuitState.Open)
                {
                    var backoffMultiplier = Math.Min(_consecutiveOpens, 5);
                    var effectiveDuration = _config.BreakDuration * Math.Pow(2, backoffMultiplier - 1);
                    if (DateTime.UtcNow - _openedAt >= effectiveDuration)
                    {
                        _state = CircuitState.HalfOpen;
                    }
                }
                return _state;
            }
        }
    }

    public bool AllowRequest()
    {
        var state = State;
        return state != CircuitState.Open;
    }

    public void RecordSuccess()
    {
        lock (_lock)
        {
            if (_state == CircuitState.HalfOpen)
            {
                _state = CircuitState.Closed;
                _consecutiveOpens = 0;
                while (_failures.TryDequeue(out _)) { }
            }
        }
    }

    public void RecordFailure()
    {
        lock (_lock)
        {
            _failures.Enqueue(DateTime.UtcNow);
            PruneOldFailures();

            if (_failures.Count >= _config.FailureThreshold)
            {
                _state = CircuitState.Open;
                _openedAt = DateTime.UtcNow;
                _consecutiveOpens++;
            }
        }
    }

    public void Reset()
    {
        lock (_lock)
        {
            _state = CircuitState.Closed;
            _consecutiveOpens = 0;
            while (_failures.TryDequeue(out _)) { }
        }
    }

    private void PruneOldFailures()
    {
        var cutoff = DateTime.UtcNow - _config.SamplingDuration;
        while (_failures.TryPeek(out var oldest) && oldest < cutoff)
        {
            _failures.TryDequeue(out _);
        }
    }
}
