using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Interfaces;

/// <summary>
/// Repository abstraction for persistent tenant quota storage.
/// Decouples <see cref="IQuotaEnforcer"/> from the concrete EF Core implementation
/// so that unit tests can mock persistence without a real database.
/// </summary>
public interface ITenantQuotaRepository
{
    /// <summary>
    /// Returns the current quota record for the given tenant.
    /// Creates a default record if none exists yet.
    /// </summary>
    Task<TenantQuotaSnapshot> GetOrCreateAsync(string tenantId, CancellationToken ct = default);

    /// <summary>
    /// Atomically increments the daily usage counters for a tenant.
    /// Uses optimistic concurrency to prevent lost updates under load.
    /// </summary>
    Task IncrementUsageAsync(string tenantId, int tokens, double costUsd, CancellationToken ct = default);

    /// <summary>
    /// Persists an updated quota configuration (limits) for a tenant.
    /// </summary>
    Task UpsertConfigAsync(string tenantId, QuotaConfig config, CancellationToken ct = default);

    /// <summary>
    /// Resets daily counters for the active tenant when its <c>LastResetAt</c> is before today UTC.
    /// A platform job must enumerate tenants and call this under each real tenant context.
    /// </summary>
    Task ResetDailyCountersAsync(string tenantId, CancellationToken ct = default);
}

/// <summary>
/// Immutable snapshot of a tenant's current quota state — safe to cache.
/// </summary>
public record TenantQuotaSnapshot(
    string TenantId,
    int RequestsPerMinute,
    long MaxTokensPerDay,
    double MaxDailyBudgetUsd,
    long CurrentDailyTokens,
    double CurrentDailyCostUsd,
    int CurrentDailyRequests);
