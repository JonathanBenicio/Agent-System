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
