namespace AgenticSystem.Core.Models;

public enum AgentStreamEventType
{
    SessionStarted,
    PlanningStarted,
    PlanningCompleted,
    AgentSelected,
    StepStarted,
    StepCompleted,
    StepFailed,
    Token,
    ToolStarted,
    ToolCompleted,
    ToolApprovalRequired,
    ToolApprovalResolved,
    HandoffStarted,
    HandoffCompleted,
    RagStarted,
    RagCompleted,
    ReviewStarted,
    ReviewCompleted,
    FinalApprovalRequired,
    FinalApprovalResolved,
    ArtifactRecorded,
    SessionCompleted,
    Error
}

public class AgentStreamEvent
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public long Sequence { get; set; }
    public string SessionId { get; set; } = string.Empty;
    public AgentStreamEventType Type { get; set; }
    public string? AgentName { get; set; }
    public string? Message { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public bool IsTerminal { get; set; }
    public Dictionary<string, object> Data { get; set; } = new();
}

public enum AgentExecutionArtifactType
{
    Plan,
    Step,
    Review,
    Handoff,
    ToolExecution,
    ToolApproval,
    FinalApproval,
    RagContext,
    SessionState,
    MetricSnapshot
}

public class AgentExecutionArtifact
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string SessionId { get; set; } = string.Empty;
    public AgentExecutionArtifactType Type { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? AgentName { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Dictionary<string, object> Data { get; set; } = new();
    public List<string> RelatedIds { get; set; } = new();
}

public enum ToolRiskLevel
{
    Low,
    Medium,
    High,
    Critical
}

public class ToolExecutionPolicy
{
    public string ToolId { get; set; } = string.Empty;
    public ToolRiskLevel RiskLevel { get; set; } = ToolRiskLevel.Low;
    public bool RequiresApproval { get; set; }
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
    public int MaxRetries { get; set; }
    public bool EnableCache { get; set; }
    public TimeSpan CacheTtl { get; set; } = TimeSpan.FromMinutes(5);
    public bool RequireIdempotencyKey { get; set; }
}

public enum ToolApprovalStatus
{
    Pending,
    Approved,
    Rejected,
    Expired
}

public class ToolApprovalRequest
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string SessionId { get; set; } = string.Empty;
    public string ToolId { get; set; } = string.Empty;
    public string ToolName { get; set; } = string.Empty;
    public string? AgentName { get; set; }
    public ToolRiskLevel RiskLevel { get; set; }
    public string Reason { get; set; } = string.Empty;
    public ToolApprovalStatus Status { get; set; } = ToolApprovalStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }
    public string? ResolvedBy { get; set; }
    public string? Comment { get; set; }
    public Dictionary<string, object> RequestedInput { get; set; } = new();
}

public class ToolExecutionDecision
{
    public bool Allowed { get; set; }
    public bool RequiresApproval { get; set; }
    public string Reason { get; set; } = string.Empty;
    public ToolExecutionPolicy Policy { get; set; } = new();
    public ToolApprovalRequest? ApprovalRequest { get; set; }
}

public enum FinalResponseApprovalStatus
{
    Pending,
    Approved,
    Rejected,
    Expired
}

public class FinalResponseApprovalRequest
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string SessionId { get; set; } = string.Empty;
    public string AgentName { get; set; } = string.Empty;
    public ToolRiskLevel RiskLevel { get; set; } = ToolRiskLevel.Medium;
    public string Reason { get; set; } = string.Empty;
    public string UserInput { get; set; } = string.Empty;
    public string ProposedResponse { get; set; } = string.Empty;
    public Dictionary<string, object> ResponseMetadata { get; set; } = new();
    public FinalResponseApprovalStatus Status { get; set; } = FinalResponseApprovalStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }
    public string? ResolvedBy { get; set; }
    public string? Comment { get; set; }
}

public class FinalResponseApprovalDecision
{
    public bool Allowed { get; set; }
    public bool RequiresApproval { get; set; }
    public string Reason { get; set; } = string.Empty;
    public FinalResponseApprovalRequest? ApprovalRequest { get; set; }
}

public class AgentRuntimeMetricsSnapshot
{
    public string? SessionId { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public long StreamCount { get; set; }
    public long AgentExecutions { get; set; }
    public long AgentFallbacks { get; set; }
    public long ToolExecutions { get; set; }
    public long ToolApprovalsRequested { get; set; }
    public long ToolApprovalsResolved { get; set; }
    public long FinalApprovalsRequested { get; set; }
    public long FinalApprovalsResolved { get; set; }
    public long Handoffs { get; set; }
    public long RagQueries { get; set; }
    public long Reviews { get; set; }
    public double AverageAgentLatencyMs { get; set; }
    public double AverageToolLatencyMs { get; set; }
    public Dictionary<string, long> EventsByType { get; set; } = new();
    public Dictionary<string, long> AgentExecutionCounts { get; set; } = new();
}