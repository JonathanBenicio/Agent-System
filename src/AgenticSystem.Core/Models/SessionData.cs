using AgenticSystem.Core.Interfaces;

namespace AgenticSystem.Core.Models;

public class SessionData
{
    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string TenantId { get; set; } = Tenant.DefaultTenantId;
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public bool IsConsolidated { get; set; }
    public List<AgentEvent> Events { get; set; } = new();
    public SessionSummary? Summary { get; set; }
    public SessionInsights? Insights { get; set; }
}
