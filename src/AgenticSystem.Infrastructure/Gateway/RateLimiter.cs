using System.Collections.Concurrent;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Infrastructure.Gateway;

/// <summary>
/// Rate Limiter baseado em sliding window (sem dependência externa).
/// </summary>
public class RateLimiter
{
    private readonly RateLimitConfig _config;
    private readonly ConcurrentQueue<DateTime> _minuteWindow = new();
    private readonly ConcurrentQueue<DateTime> _hourWindow = new();
    private long _dailyTokens;
    private DateTime _dailyReset;
    private readonly object _lock = new();

    public RateLimiter(RateLimitConfig config)
    {
        _config = config;
        _dailyReset = DateTime.UtcNow.Date.AddDays(1);
    }

    public bool AllowRequest()
    {
        lock (_lock)
        {
            ResetDailyIfNeeded();
            PruneWindows();

            return _minuteWindow.Count < _config.RequestsPerMinute &&
                   _hourWindow.Count < _config.RequestsPerHour;
        }
    }

    public bool AllowTokens(int tokens)
    {
        lock (_lock)
        {
            ResetDailyIfNeeded();
            return _dailyTokens + tokens <= _config.TokensPerDay;
        }
    }

    public void RecordRequest(int tokens = 0)
    {
        lock (_lock)
        {
            var now = DateTime.UtcNow;
            _minuteWindow.Enqueue(now);
            _hourWindow.Enqueue(now);
            _dailyTokens += tokens;
        }
    }

    public RateLimitStatus GetStatus()
    {
        lock (_lock)
        {
            PruneWindows();
            return new RateLimitStatus
            {
                MinuteUsed = _minuteWindow.Count,
                MinuteLimit = _config.RequestsPerMinute,
                HourUsed = _hourWindow.Count,
                HourLimit = _config.RequestsPerHour,
                DailyTokensUsed = _dailyTokens,
                DailyTokensLimit = _config.TokensPerDay
            };
        }
    }

    private void PruneWindows()
    {
        var now = DateTime.UtcNow;
        var minuteAgo = now.AddMinutes(-1);
        var hourAgo = now.AddHours(-1);

        while (_minuteWindow.TryPeek(out var t) && t < minuteAgo)
            _minuteWindow.TryDequeue(out _);

        while (_hourWindow.TryPeek(out var t) && t < hourAgo)
            _hourWindow.TryDequeue(out _);
    }

    private void ResetDailyIfNeeded()
    {
        if (DateTime.UtcNow >= _dailyReset)
        {
            _dailyTokens = 0;
            _dailyReset = DateTime.UtcNow.Date.AddDays(1);
        }
    }
}

public class RateLimitStatus
{
    public int MinuteUsed { get; set; }
    public int MinuteLimit { get; set; }
    public int HourUsed { get; set; }
    public int HourLimit { get; set; }
    public long DailyTokensUsed { get; set; }
    public int DailyTokensLimit { get; set; }
}
