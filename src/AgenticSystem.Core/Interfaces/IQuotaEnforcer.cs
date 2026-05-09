using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Interfaces;

/// <summary>
/// Quota enforcement service with real-time tracking and automatic degradation.
/// </summary>
public interface IQuotaEnforcer
{
    /// <summary>
    /// Checks if a request is allowed under current quotas.
    /// Returns false if the quota is exceeded.
    /// </summary>
    Task<QuotaCheckResult> CheckQuotaAsync(
        string ownerId,
        int estimatedTokens = 0,
        double estimatedCostUsd = 0,
        CancellationToken ct = default);

    /// <summary>
    /// Records usage against the quota.
    /// </summary>
    Task RecordUsageAsync(
        string ownerId,
        int tokensUsed,
        double costUsd,
        CancellationToken ct = default);

    /// <summary>
    /// Returns current usage snapshot.
    /// </summary>
    Task<QuotaUsage> GetUsageAsync(
        string ownerId,
        CancellationToken ct = default);

    /// <summary>
    /// Sets or updates quota configuration.
    /// </summary>
    Task SetQuotaConfigAsync(
        QuotaConfig config,
        CancellationToken ct = default);

    /// <summary>
    /// Returns quota config for an owner.
    /// </summary>
    Task<QuotaConfig?> GetQuotaConfigAsync(
        string ownerId,
        CancellationToken ct = default);
}

/// <summary>
/// Result of a quota check.
/// </summary>
public class QuotaCheckResult
{
    public bool Allowed { get; init; }
    public string? DenialReason { get; init; }
    public QuotaAlertAction? RecommendedAction { get; init; }
    public double UsagePercent { get; init; }
    public string? AlertMessage { get; init; }
}
