using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Services;

/// <summary>
/// Production implementation of <see cref="IQuotaEnforcer"/> that combines:
/// <list type="bullet">
///   <item>An <see cref="IMemoryCache"/> with a 60-second TTL for hot-path reads (rate-limit checks),</item>
///   <item>An <see cref="ITenantQuotaRepository"/> for durable writes and daily-reset persistence.</item>
/// </list>
/// This dual-layer design avoids hitting the database on every token-counted LLM call while
/// ensuring quotas survive application restarts and work correctly across multiple replicas.
/// </summary>
public class QuotaEnforcer : IQuotaEnforcer
{
    private readonly IMemoryCache _cache;
    private readonly ITenantQuotaRepository _repository;
    private readonly ILogger<QuotaEnforcer> _logger;

    /// <summary>
    /// In-memory per-minute sliding window counters. These are intentionally NOT persisted
    /// because per-minute rate-limiting is a per-instance concern (requests are sticky per pod
    /// in the default deployment) and the data is obsolete within 60 seconds anyway.
    /// </summary>
    private readonly ConcurrentDictionary<string, (int Count, DateTime WindowStart)> _minuteCounters = new();

    private static readonly MemoryCacheEntryOptions CacheOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60),
        Priority = CacheItemPriority.High,
    };

    public QuotaEnforcer(
        IMemoryCache cache,
        ITenantQuotaRepository repository,
        ILogger<QuotaEnforcer> logger)
    {
        _cache = cache;
        _repository = repository;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<QuotaCheckResult> CheckQuotaAsync(
        string ownerId,
        int estimatedTokens = 0,
        double estimatedCostUsd = 0,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(ownerId))
            return new QuotaCheckResult { Allowed = true };

        // 1. Per-minute rate-limit check (in-memory, no DB trip needed)
        var minuteWindow = GetOrResetMinuteWindow(ownerId);
        var snapshot = await GetSnapshotAsync(ownerId, ct);

        if (minuteWindow.Count >= snapshot.RequestsPerMinute)
        {
            return new QuotaCheckResult
            {
                Allowed = false,
                DenialReason = "Rate limit exceeded (requests per minute)",
                RecommendedAction = QuotaAlertAction.Block,
                UsagePercent = 100
            };
        }

        // 2. Daily token quota check (from cache → DB fallback)
        if (snapshot.MaxTokensPerDay > 0 && snapshot.CurrentDailyTokens + estimatedTokens > snapshot.MaxTokensPerDay)
        {
            return new QuotaCheckResult
            {
                Allowed = false,
                DenialReason = "Daily token quota exceeded",
                RecommendedAction = QuotaAlertAction.Block,
                UsagePercent = CalculatePercent(snapshot.CurrentDailyTokens, snapshot.MaxTokensPerDay)
            };
        }

        // 3. Daily budget check
        if (snapshot.MaxDailyBudgetUsd > 0 && snapshot.CurrentDailyCostUsd + estimatedCostUsd > snapshot.MaxDailyBudgetUsd)
        {
            return new QuotaCheckResult
            {
                Allowed = false,
                DenialReason = "Daily budget exceeded",
                RecommendedAction = QuotaAlertAction.Block,
                UsagePercent = CalculatePercent(snapshot.CurrentDailyCostUsd, snapshot.MaxDailyBudgetUsd)
            };
        }

        var usagePercent = CalculatePercent(
            Math.Max(
                snapshot.MaxTokensPerDay > 0 ? (double)snapshot.CurrentDailyTokens / snapshot.MaxTokensPerDay : 0,
                snapshot.MaxDailyBudgetUsd > 0 ? snapshot.CurrentDailyCostUsd / snapshot.MaxDailyBudgetUsd : 0),
            1.0);

        return new QuotaCheckResult { Allowed = true, UsagePercent = usagePercent };
    }

    /// <inheritdoc/>
    public async Task RecordUsageAsync(
        string ownerId,
        int tokensUsed,
        double costUsd,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(ownerId)) return;

        // Increment in-memory per-minute counter
        IncrementMinuteWindow(ownerId);

        // Persist to database — this is the source of truth for daily totals.
        await _repository.IncrementUsageAsync(ownerId, tokensUsed, costUsd, ct);

        // Invalidate cache so the next check reflects real DB values immediately.
        _cache.Remove(CacheKey(ownerId));

        _logger.LogDebug("Recorded usage for {OwnerId}: {Tokens} tokens, ${Cost:F4}", ownerId, tokensUsed, costUsd);
    }

    /// <inheritdoc/>
    public async Task<QuotaUsage> GetUsageAsync(string ownerId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(ownerId))
            return new QuotaUsage { OwnerId = ownerId };

        var snapshot = await GetSnapshotAsync(ownerId, ct);
        var minuteWindow = GetOrResetMinuteWindow(ownerId);

        return new QuotaUsage
        {
            OwnerId = ownerId,
            TokensToday = (int)Math.Min(snapshot.CurrentDailyTokens, int.MaxValue),
            CostToday = snapshot.CurrentDailyCostUsd,
            RequestsToday = snapshot.CurrentDailyRequests,
            RequestsThisMinute = minuteWindow.Count,
            PeriodStart = DateTime.UtcNow.Date
        };
    }

    /// <inheritdoc/>
    public async Task SetQuotaConfigAsync(QuotaConfig config, CancellationToken ct = default)
    {
        await _repository.UpsertConfigAsync(config.OwnerId, config, ct);
        _cache.Remove(CacheKey(config.OwnerId));
    }

    /// <inheritdoc/>
    public async Task<QuotaConfig?> GetQuotaConfigAsync(string ownerId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(ownerId)) return null;
        var snapshot = await GetSnapshotAsync(ownerId, ct);

        return new QuotaConfig
        {
            OwnerId = snapshot.TenantId,
            RequestsPerMinute = snapshot.RequestsPerMinute,
            MaxTokensPerDay = snapshot.MaxTokensPerDay,
            MaxDailyBudgetUsd = snapshot.MaxDailyBudgetUsd
        };
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task<TenantQuotaSnapshot> GetSnapshotAsync(string ownerId, CancellationToken ct)
    {
        return await _cache.GetOrCreateAsync(
            CacheKey(ownerId),
            async entry =>
            {
                entry.SetOptions(CacheOptions);
                return await _repository.GetOrCreateAsync(ownerId, ct);
            }) ?? await _repository.GetOrCreateAsync(ownerId, ct);
    }

    private (int Count, DateTime WindowStart) GetOrResetMinuteWindow(string ownerId)
    {
        var now = DateTime.UtcNow;
        return _minuteCounters.AddOrUpdate(
            ownerId,
            _ => (0, now),
            (_, existing) => existing.WindowStart.AddMinutes(1) < now
                ? (0, now)
                : existing);
    }

    private void IncrementMinuteWindow(string ownerId)
    {
        var now = DateTime.UtcNow;
        _minuteCounters.AddOrUpdate(
            ownerId,
            _ => (1, now),
            (_, existing) => existing.WindowStart.AddMinutes(1) < now
                ? (1, now)
                : (existing.Count + 1, existing.WindowStart));
    }

    private static string CacheKey(string ownerId) => $"quota:{ownerId}";

    private static double CalculatePercent(double current, double max) =>
        max <= 0 ? 0 : Math.Min(100, current / max * 100);
}
