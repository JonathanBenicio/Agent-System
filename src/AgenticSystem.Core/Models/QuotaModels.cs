namespace AgenticSystem.Core.Models;

// ═══════════════════════════════════════════════════════════
// Quotas & Rate Limiting — Enforcement & Alerts
// ═══════════════════════════════════════════════════════════

/// <summary>
/// Quota configuration for a tenant or user.
/// </summary>
public class QuotaConfig
{
    public string OwnerId { get; init; } = string.Empty;
    public QuotaOwnerType OwnerType { get; init; } = QuotaOwnerType.Tenant;
    public int RequestsPerMinute { get; init; } = 60;
    public int RequestsPerHour { get; init; } = 1000;
    public int RequestsPerDay { get; init; } = 10000;
    public int MaxTokensPerRequest { get; init; } = 8000;
    public int MaxTokensPerDay { get; init; } = 1_000_000;
    public double MaxDailyBudgetUsd { get; init; } = 50;
    public double MaxMonthlyBudgetUsd { get; init; } = 500;
    public List<QuotaAlert> Alerts { get; init; } = [];
}

public enum QuotaOwnerType
{
    User,
    Tenant,
    Agent,
    Global
}

/// <summary>
/// Alert threshold for quota monitoring.
/// </summary>
public class QuotaAlert
{
    public string MetricName { get; init; } = string.Empty; // "requests", "tokens", "cost"
    public double ThresholdPercent { get; init; } = 80;      // Alert at 80% usage
    public QuotaAlertAction Action { get; init; } = QuotaAlertAction.Notify;
}

public enum QuotaAlertAction
{
    Notify,    // Send notification only
    Throttle,  // Reduce rate limit
    Degrade,   // Switch to cheaper model
    Block      // Block requests
}

/// <summary>
/// Current usage snapshot for quota enforcement.
/// </summary>
public class QuotaUsage
{
    public string OwnerId { get; init; } = string.Empty;
    public int RequestsThisMinute { get; set; }
    public int RequestsThisHour { get; set; }
    public int RequestsToday { get; set; }
    public int TokensToday { get; set; }
    public double CostToday { get; set; }
    public double CostThisMonth { get; set; }
    public DateTime PeriodStart { get; init; } = DateTime.UtcNow.Date;
    public bool IsOverLimit { get; set; }
    public string? ActiveAlertMessage { get; set; }
}

/// <summary>
/// Real-time quota and balance tracking for external LLM providers.
/// </summary>
public class ExternalProviderQuota
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// The provider name (OpenAI, Claude, Gemini, OpenRouter, etc.)
    /// </summary>
    public string ProviderName { get; init; } = string.Empty;

    /// <summary>
    /// Optional Tenant link (BYOK). If null, it's a global infrastructure key.
    /// </summary>
    public string? TenantId { get; init; }

    /// <summary>
    /// The actual API Key ID or Hash for identification.
    /// </summary>
    public string ApiKeyId { get; init; } = string.Empty;

    // Rate Limits (Reactive/Headers)
    public long RemainingRequests { get; set; }
    public long RemainingTokens { get; set; }
    public DateTime? ResetAt { get; set; }

    // Billing (Proactive/Sync)
    public double BalanceRemaining { get; set; }
    public string Currency { get; set; } = "USD";
    public DateTime LastSyncAt { get; set; } = DateTime.UtcNow;

    public bool IsExhausted => RemainingRequests <= 0 || (RemainingTokens <= 0 && RemainingTokens != -1) || BalanceRemaining <= 0;
}
