using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Services;

/// <summary>
/// Production implementation of <see cref="IQuotaEnforcer"/> that combines:
/// Daily usage and configuration are always read from the durable repository so stale replica
/// snapshots cannot authorize usage beyond the persisted quota.
/// </summary>
public class QuotaEnforcer : IQuotaEnforcer
{
    private readonly ITenantQuotaRepository _repository;
    private readonly ILogger<QuotaEnforcer> _logger;
    private readonly ITenantContextAccessor? _tenantContextAccessor;

    /// <summary>
    /// In-memory per-minute sliding window counters. These are intentionally NOT persisted
    /// because per-minute rate-limiting is a per-instance concern (requests are sticky per pod
    /// in the default deployment) and the data is obsolete within 60 seconds anyway.
    /// </summary>
    private readonly ConcurrentDictionary<string, (int Count, DateTime WindowStart)> _minuteCounters = new();

    public QuotaEnforcer(
        IMemoryCache cache,
        ITenantQuotaRepository repository,
        ILogger<QuotaEnforcer> logger,
        ITenantContextAccessor? tenantContextAccessor = null)
    {
        ArgumentNullException.ThrowIfNull(cache);
        _repository = repository;
        _logger = logger;
        _tenantContextAccessor = tenantContextAccessor;
    }

    /// <inheritdoc/>
    public async Task<QuotaCheckResult> CheckQuotaAsync(
        string ownerId,
        int estimatedTokens = 0,
        double estimatedCostUsd = 0,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(ownerId))
            return new QuotaCheckResult
            {
                Allowed = false,
                DenialReason = "A tenant identity is required to enforce usage limits.",
                RecommendedAction = QuotaAlertAction.Block
            };

        // 1. Per-minute rate-limit check (in-memory, no DB trip needed)
        var minuteWindow = GetOrResetMinuteWindow(ownerId);
        var snapshot = ApplyTenantPlanCeiling(ownerId, await GetSnapshotAsync(ownerId, ct));

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
        return await _repository.GetOrCreateAsync(ownerId, ct);
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

    private TenantQuotaSnapshot ApplyTenantPlanCeiling(string tenantId, TenantQuotaSnapshot snapshot)
    {
        var tenant = _tenantContextAccessor?.CurrentContext;
        if (tenant is null || !string.Equals(tenant.TenantId, tenantId, StringComparison.Ordinal))
            return snapshot;

        var ceiling = tenant.Plan switch
        {
            TenantPlan.Pro => TenantLimits.ProTier(),
            TenantPlan.Enterprise => TenantLimits.EnterpriseTier(),
            _ => TenantLimits.FreeTier()
        };
        var configured = tenant.Limits;
        var requestsPerMinute = Restrict(snapshot.RequestsPerMinute, Math.Min(ceiling.MaxRequestsPerMinute, PositiveOr(configured.MaxRequestsPerMinute, ceiling.MaxRequestsPerMinute)));
        var tokensPerDay = Restrict(snapshot.MaxTokensPerDay, Math.Min(ceiling.MaxTokensPerDay, PositiveOr(configured.MaxTokensPerDay, ceiling.MaxTokensPerDay)));
        var budgetPerDay = Restrict(snapshot.MaxDailyBudgetUsd, Math.Min((double)ceiling.MaxDailyCostUsd, PositiveOr((double)configured.MaxDailyCostUsd, (double)ceiling.MaxDailyCostUsd)));

        return snapshot with
        {
            RequestsPerMinute = requestsPerMinute,
            MaxTokensPerDay = tokensPerDay,
            MaxDailyBudgetUsd = budgetPerDay
        };
    }

    private static int PositiveOr(int value, int fallback) => value > 0 ? value : fallback;
    private static double PositiveOr(double value, double fallback) => value > 0 ? value : fallback;
    private static int Restrict(int configured, int ceiling) => configured > 0 ? Math.Min(configured, ceiling) : ceiling;
    private static long Restrict(long configured, int ceiling) => configured > 0 ? Math.Min(configured, ceiling) : ceiling;
    private static double Restrict(double configured, double ceiling) => configured > 0 ? Math.Min(configured, ceiling) : ceiling;

    private static double CalculatePercent(double current, double max) =>
        max <= 0 ? 0 : Math.Min(100, current / max * 100);
}
