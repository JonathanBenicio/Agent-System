namespace AgenticSystem.Core.Models;

// ═══════════════════════════════════════════════════════════
// Policy Engine — Declarative agent policies
// ═══════════════════════════════════════════════════════════

/// <summary>
/// Autonomy level for an agent — controls how much it can do without human approval.
/// </summary>
public enum AutonomyLevel
{
    /// <summary>L0 — Fully manual: agent only suggests, human executes.</summary>
    Manual = 0,
    /// <summary>L1 — Agent acts on read-only operations, approves writes.</summary>
    Assisted = 1,
    /// <summary>L2 — Agent acts on low-risk, asks approval on medium+.</summary>
    Supervised = 2,
    /// <summary>L3 — Agent acts on low+medium, asks approval on high+critical.</summary>
    SemiAutonomous = 3,
    /// <summary>L4 — Agent acts on all but critical, alerts on critical.</summary>
    Autonomous = 4,
    /// <summary>L5 — Agent acts on everything, logs only. Use with extreme caution.</summary>
    FullAutonomy = 5
}

/// <summary>
/// Declarative policy for an agent — defines what it can and cannot do.
/// </summary>
public class AgentPolicy
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>Agent name pattern this policy applies to. Null = all agents.</summary>
    public string? AgentNamePattern { get; set; }

    /// <summary>Tenant ID this policy applies to. Null = all tenants.</summary>
    public string? TenantId { get; set; }

    /// <summary>Maximum autonomy level allowed.</summary>
    public AutonomyLevel MaxAutonomyLevel { get; set; } = AutonomyLevel.Supervised;

    /// <summary>Allowed tool categories. Empty = all allowed.</summary>
    public List<string> AllowedToolCategories { get; set; } = new();

    /// <summary>Denied tool names (blocklist, overrides allow).</summary>
    public List<string> DeniedTools { get; set; } = new();

    /// <summary>Allowed LLM providers. Empty = all allowed.</summary>
    public List<string> AllowedProviders { get; set; } = new();

    /// <summary>Maximum cost per request in USD.</summary>
    public decimal? MaxCostPerRequest { get; set; }

    /// <summary>Maximum cost per day in USD.</summary>
    public decimal? MaxCostPerDay { get; set; }

    /// <summary>Maximum tokens per request.</summary>
    public int? MaxTokensPerRequest { get; set; }

    /// <summary>Whether final response requires human approval.</summary>
    public bool RequireFinalApproval { get; set; }

    /// <summary>Risk level threshold that triggers approval.</summary>
    public ToolRiskLevel ApprovalThreshold { get; set; } = ToolRiskLevel.High;

    /// <summary>Content filters to apply (e.g., "no-pii", "no-code-execution").</summary>
    public List<string> ContentFilters { get; set; } = new();

    /// <summary>Priority for evaluation ordering (higher = evaluated first).</summary>
    public int Priority { get; set; }

    /// <summary>Whether this policy is active.</summary>
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// Context provided to the policy engine for evaluation.
/// </summary>
public class PolicyContext
{
    public string? AgentName { get; set; }
    public string? TenantId { get; set; }
    public string? UserId { get; set; }
    public string? ToolName { get; set; }
    public string? ToolCategory { get; set; }
    public string? Action { get; set; }
    public ToolRiskLevel RiskLevel { get; set; }
    public decimal? EstimatedCost { get; set; }
    public int? EstimatedTokens { get; set; }
    public string? Provider { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Result of policy evaluation.
/// </summary>
public class PolicyEvaluation
{
    public bool Allowed { get; set; }
    public bool RequiresApproval { get; set; }
    public string Reason { get; set; } = string.Empty;
    public List<PolicyViolation> Violations { get; set; } = new();
    public AgentPolicy? MatchedPolicy { get; set; }
    public AutonomyLevel EffectiveAutonomyLevel { get; set; }
    public DateTime EvaluatedAt { get; set; } = DateTime.UtcNow;

    public static PolicyEvaluation Allow(string reason, AgentPolicy? policy = null) => new()
    {
        Allowed = true,
        Reason = reason,
        MatchedPolicy = policy,
        EffectiveAutonomyLevel = policy?.MaxAutonomyLevel ?? AutonomyLevel.Supervised
    };

    public static PolicyEvaluation Deny(string reason, List<PolicyViolation>? violations = null, AgentPolicy? policy = null) => new()
    {
        Allowed = false,
        Reason = reason,
        Violations = violations ?? new(),
        MatchedPolicy = policy
    };

    public static PolicyEvaluation RequireApproval(string reason, AgentPolicy? policy = null) => new()
    {
        Allowed = false,
        RequiresApproval = true,
        Reason = reason,
        MatchedPolicy = policy
    };
}

/// <summary>
/// Describes a specific policy violation.
/// </summary>
public class PolicyViolation
{
    public string PolicyId { get; set; } = string.Empty;
    public string PolicyName { get; set; } = string.Empty;
    public PolicyViolationType Type { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? ActualValue { get; set; }
    public string? AllowedValue { get; set; }
}

public enum PolicyViolationType
{
    ToolDenied,
    CategoryDenied,
    ProviderDenied,
    BudgetExceeded,
    TokenLimitExceeded,
    AutonomyExceeded,
    ContentFilterViolation,
    ApprovalRequired
}
