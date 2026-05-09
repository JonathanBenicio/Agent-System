namespace AgenticSystem.Core.Models;

// ═══════════════════════════════════════════════════════════
// #31 — Autonomy Levels
// ═══════════════════════════════════════════════════════════

/// <summary>
/// Autonomy configuration for an agent, defining what it can do independently.
/// </summary>
public class AutonomyConfig
{
    public string AgentName { get; init; } = string.Empty;
    public AutonomyLevel Level { get; init; } = AutonomyLevel.Assisted;
    public double MaxCostPerActionUsd { get; init; } = 1.0;
    public int MaxToolCallsPerTurn { get; init; } = 10;
    public List<string> AutoApprovedTools { get; init; } = [];
    public List<string> AlwaysRequireApprovalTools { get; init; } = [];
    public bool AllowExternalApiCalls { get; init; } = false;
    public bool AllowDataModification { get; init; } = false;
    public TimeSpan MaxExecutionTime { get; init; } = TimeSpan.FromMinutes(5);
}

// ═══════════════════════════════════════════════════════════
// #32 — Dynamic Risk Scoring
// ═══════════════════════════════════════════════════════════

/// <summary>
/// Dynamic risk assessment for an agent action.
/// </summary>
public class RiskAssessment
{
    public string ActionId { get; init; } = Guid.NewGuid().ToString("N");
    public string AgentName { get; init; } = string.Empty;
    public string ActionDescription { get; init; } = string.Empty;
    public double RiskScore { get; init; } // 0.0 (safe) → 1.0 (critical)
    public RiskCategory Category { get; init; }
    public List<RiskFactor> Factors { get; init; } = [];
    public string Recommendation { get; init; } = string.Empty;
    public bool RequiresApproval { get; init; }
    public DateTime AssessedAt { get; init; } = DateTime.UtcNow;
}

public enum RiskCategory
{
    DataAccess,
    DataModification,
    ExternalCommunication,
    FinancialTransaction,
    SystemConfiguration,
    UserImpersonation,
    PrivilegedOperation
}

public class RiskFactor
{
    public string Name { get; init; } = string.Empty;
    public double Weight { get; init; }
    public double Score { get; init; }
    public string Description { get; init; } = string.Empty;
}

// ═══════════════════════════════════════════════════════════
// #33 — Simulation Mode
// ═══════════════════════════════════════════════════════════

/// <summary>
/// Configuration for dry-run / simulation mode.
/// </summary>
public class SimulationConfig
{
    public bool DryRun { get; init; } = true;
    public bool LogAllActions { get; init; } = true;
    public bool EstimateCosts { get; init; } = true;
    public bool SimulateLatency { get; init; } = false;
    public Dictionary<string, string> MockResponses { get; init; } = new();
}

/// <summary>
/// Result of a simulated execution.
/// </summary>
public class SimulationResult
{
    public string SimulationId { get; init; } = Guid.NewGuid().ToString("N");
    public List<SimulatedAction> Actions { get; init; } = [];
    public double EstimatedCostUsd { get; init; }
    public int EstimatedTokens { get; init; }
    public TimeSpan EstimatedDuration { get; init; }
    public List<string> Warnings { get; init; } = [];
    public bool WouldSucceed { get; init; }
}

public class SimulatedAction
{
    public int Sequence { get; init; }
    public string ActionType { get; init; } = string.Empty; // "tool_call", "llm_call", "rag_query"
    public string Description { get; init; } = string.Empty;
    public double EstimatedCostUsd { get; init; }
    public string? MockResult { get; init; }
    public RiskCategory? RiskCategory { get; init; }
}

// ═══════════════════════════════════════════════════════════
// #34 — Self-Improvement
// ═══════════════════════════════════════════════════════════

/// <summary>
/// Agent self-improvement record based on feedback and corrections.
/// </summary>
public class SelfImprovementRecord
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string AgentName { get; init; } = string.Empty;
    public ImprovementType Type { get; init; }
    public string OriginalBehavior { get; init; } = string.Empty;
    public string ImprovedBehavior { get; init; } = string.Empty;
    public string Trigger { get; init; } = string.Empty; // What caused the improvement
    public double ConfidenceGain { get; init; }
    public DateTime LearnedAt { get; init; } = DateTime.UtcNow;
    public bool Applied { get; set; }
    public string Status { get; set; } = "Proposed";
    public string? Rationale { get; set; }
    public Dictionary<string, string> ProposedChanges { get; init; } = new();
}

public enum ImprovementType
{
    PromptRefinement,
    PromptOptimization,
    ToolUsagePattern,
    ResponseFormat,
    ErrorRecovery,
    DomainKnowledge,
    UserPreference
}
