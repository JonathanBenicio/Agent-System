using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AgenticSystem.Core.Interfaces;

namespace AgenticSystem.Infrastructure.Persistence.Entities;

/// <summary>
/// Persistent storage for tenant quota configuration and current daily usage.
/// Replaces the in-memory ConcurrentDictionary in QuotaEnforcer with a durable
/// PostgreSQL-backed store that survives application restarts and supports
/// horizontal scaling.
/// </summary>
[Table("tenant_quotas")]
public class TenantQuotaEntity : ITenantEntity
{
    [Key]
    [MaxLength(100)]
    public string TenantId { get; set; } = string.Empty;

    /// <summary>Maximum number of requests allowed per minute.</summary>
    public int RequestsPerMinute { get; set; } = 30;

    /// <summary>Maximum tokens allowed per day.</summary>
    public long MaxTokensPerDay { get; set; } = 1_000_000;

    /// <summary>Maximum budget in USD allowed per day.</summary>
    public double MaxDailyBudgetUsd { get; set; } = 50.00;

    /// <summary>Tokens consumed today (UTC day).</summary>
    public long CurrentDailyTokens { get; set; } = 0;

    /// <summary>Cost consumed today in USD (UTC day).</summary>
    public double CurrentDailyCostUsd { get; set; } = 0;

    /// <summary>Total requests made today.</summary>
    public int CurrentDailyRequests { get; set; } = 0;

    /// <summary>UTC timestamp when the daily counters were last reset.</summary>
    public DateTime LastResetAt { get; set; } = DateTime.UtcNow.Date;

    /// <summary>UTC timestamp of the last write to this record.</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Concurrency token for optimistic locking — prevents lost updates under
    /// concurrent requests incrementing the usage counters simultaneously.
    /// </summary>
    [Timestamp]
    public uint RowVersion { get; set; }
}
