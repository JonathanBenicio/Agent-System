namespace AgenticSystem.Core.Models;

public class TokenUsageRecord
{
    public string SessionId { get; set; } = string.Empty;
    public string TenantId { get; set; } = "default";
    public string AgentName { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string ModelId { get; set; } = string.Empty;
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int CachedTokens { get; set; }
    public decimal CalculatedCost { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class LlmPricingRule
{
    public string Id { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string ModelId { get; set; } = string.Empty;
    public decimal CostPerMillionPromptTokens { get; set; }
    public decimal CostPerMillionCompletionTokens { get; set; }
    public decimal CostPerMillionCachedTokens { get; set; }
    public DateTime EffectiveDate { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
}

public class TurnCostSummary
{
    public string SessionId { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public decimal AccumulatedSessionCost { get; set; }
    public decimal LastTurnCost { get; set; }
    public int TotalPromptTokens { get; set; }
    public int TotalCompletionTokens { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
