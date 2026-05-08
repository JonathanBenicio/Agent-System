namespace AgenticSystem.Core.Models;

// ═══════════════════════════════════════════════════════════
// Centralized Audit Log
// ═══════════════════════════════════════════════════════════

/// <summary>
/// Immutable audit entry for any significant system action.
/// </summary>
public class AuditEntry
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public AuditCategory Category { get; init; }
    public string Action { get; init; } = string.Empty;
    public string? UserId { get; init; }
    public string? TenantId { get; init; }
    public string? SessionId { get; init; }
    public string? AgentName { get; init; }
    public string? ToolName { get; init; }
    public string? ModelUsed { get; init; }
    public decimal? Cost { get; init; }
    public string? TraceId { get; init; }
    public string? Description { get; init; }
    public bool Success { get; init; } = true;
    public string? ErrorMessage { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public Dictionary<string, object> Metadata { get; init; } = new();
}

public enum AuditCategory
{
    AgentExecution,
    ToolCall,
    DataAccess,
    ConfigChange,
    PolicyViolation,
    ApprovalDecision,
    Authentication,
    PermissionChange,
    KnowledgeIngestion,
    SystemEvent
}

/// <summary>
/// Query filter for audit log retrieval.
/// </summary>
public class AuditQuery
{
    public AuditCategory? Category { get; set; }
    public string? UserId { get; set; }
    public string? TenantId { get; set; }
    public string? SessionId { get; set; }
    public string? AgentName { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public bool? SuccessOnly { get; set; }
    public int Limit { get; set; } = 100;
    public int Offset { get; set; }
}
