using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Services;

/// <summary>
/// Concrete implementation of IQuotaEnforcer using in-memory tracking.
/// </summary>
public class QuotaEnforcer : IQuotaEnforcer
{
    private readonly ConcurrentDictionary<string, QuotaConfig> _configs = new();
    private readonly ConcurrentDictionary<string, QuotaUsage> _usage = new();
    private readonly ILogger<QuotaEnforcer> _logger;

    public QuotaEnforcer(ILogger<QuotaEnforcer> logger)
    {
        _logger = logger;
    }

    public Task<QuotaCheckResult> CheckQuotaAsync(
        string ownerId,
        int estimatedTokens = 0,
        double estimatedCostUsd = 0,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(ownerId))
        {
            return Task.FromResult(new QuotaCheckResult { Allowed = true });
        }

        var config = _configs.GetOrAdd(ownerId, id => new QuotaConfig { OwnerId = id });
        var usage = GetOrCreateUsage(ownerId);

        // Reset periods if necessary
        ResetUsagePeriods(usage);

        // 1. Check Requests per minute
        if (usage.RequestsThisMinute >= config.RequestsPerMinute)
        {
            return Task.FromResult(new QuotaCheckResult
            {
                Allowed = false,
                DenialReason = "Rate limit exceeded (Requests per minute)",
                RecommendedAction = QuotaAlertAction.Block
            });
        }

        // 2. Check Daily Tokens
        if (usage.TokensToday + estimatedTokens > config.MaxTokensPerDay)
        {
            return Task.FromResult(new QuotaCheckResult
            {
                Allowed = false,
                DenialReason = "Daily token quota exceeded",
                RecommendedAction = QuotaAlertAction.Block
            });
        }

        // 3. Check Daily Budget
        if (usage.CostToday + estimatedCostUsd > config.MaxDailyBudgetUsd)
        {
            return Task.FromResult(new QuotaCheckResult
            {
                Allowed = false,
                DenialReason = "Daily budget exceeded",
                RecommendedAction = QuotaAlertAction.Block
            });
        }

        return Task.FromResult(new QuotaCheckResult { Allowed = true });
    }

    public Task RecordUsageAsync(
        string ownerId,
        int tokensUsed,
        double costUsd,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(ownerId)) return Task.CompletedTask;

        var usage = GetOrCreateUsage(ownerId);
        lock (usage)
        {
            ResetUsagePeriods(usage);
            usage.RequestsThisMinute++;
            usage.RequestsThisHour++;
            usage.RequestsToday++;
            usage.TokensToday += tokensUsed;
            usage.CostToday += costUsd;
            usage.CostThisMonth += costUsd;
        }

        _logger.LogDebug("Recorded usage for {OwnerId}: {Tokens} tokens, ${Cost}", ownerId, tokensUsed, costUsd);
        return Task.CompletedTask;
    }

    public Task<QuotaUsage> GetUsageAsync(string ownerId, CancellationToken ct = default)
    {
        return Task.FromResult(GetOrCreateUsage(ownerId));
    }

    public Task SetQuotaConfigAsync(QuotaConfig config, CancellationToken ct = default)
    {
        _configs[config.OwnerId] = config;
        return Task.CompletedTask;
    }

    public Task<QuotaConfig?> GetQuotaConfigAsync(string ownerId, CancellationToken ct = default)
    {
        _configs.TryGetValue(ownerId, out var config);
        return Task.FromResult(config);
    }

    private QuotaUsage GetOrCreateUsage(string ownerId)
    {
        return _usage.GetOrAdd(ownerId, id => new QuotaUsage
        {
            OwnerId = id,
            PeriodStart = DateTime.UtcNow.Date
        });
    }

    private void ResetUsagePeriods(QuotaUsage usage)
    {
        var now = DateTime.UtcNow;
        // In a real implementation, we'd track the minute/hour/day timestamps.
        // For simplicity in this lab version, we just check against a single Daily PeriodStart.
        if (usage.PeriodStart < now.Date)
        {
            lock (usage)
            {
                if (usage.PeriodStart < now.Date)
                {
                    usage.RequestsToday = 0;
                    usage.TokensToday = 0;
                    usage.CostToday = 0;
                    // usage.PeriodStart = now.Date; // Need to handle month reset too
                }
            }
        }
    }
}
