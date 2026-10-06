using AgenticSystem.Core.Interfaces;

namespace AgenticSystem.Infrastructure.Persistence.Entities;

public sealed class SelfImprovementProposalEntity : ITenantEntity
{
    public string Id { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string AgentName { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Status { get; set; } = "Proposed";
    public double ConfidenceLevel { get; set; }
    public string Rationale { get; set; } = string.Empty;
    public string ProposedChangesJson { get; set; } = "{}";
    public string? PreviousInstructions { get; set; }
    public string? CreatedBy { get; set; }
    public string? ReviewedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedAt { get; set; }
    public int? AppliedPromptVersion { get; set; }
    public string? AppliedAgentVersionId { get; set; }
}
