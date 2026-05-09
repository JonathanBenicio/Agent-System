using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Interfaces;

/// <summary>
/// #42 — Dependency Graph service for impact analysis.
/// </summary>
public interface IDependencyGraph
{
    Task<DependencyNode> GetNodeAsync(string id, CancellationToken ct = default);
    Task RegisterDependencyAsync(string sourceId, string targetId, CancellationToken ct = default);
    Task<ImpactAnalysis> AnalyzeImpactAsync(string changedNodeId, CancellationToken ct = default);
}

/// <summary>
/// #43 — AG-UI Runtime service for dynamic UI generation.
/// </summary>
public interface IAgUiRuntime
{
    Task<AgentUIComponent> GenerateComponentAsync(string agentName, string context, CancellationToken ct = default);
    Task<string> RenderHtmlAsync(AgentUIComponent component, CancellationToken ct = default);
}

/// <summary>
/// #44 — SLA / QoS management service.
/// </summary>
public interface ISlaManager
{
    Task<SlaTier> GetTierAsync(string tenantId, CancellationToken ct = default);
    Task SetTierAsync(string tenantId, string tierId, CancellationToken ct = default);
    Task<bool> EnsureComplianceAsync(string tenantId, double latencyMs, CancellationToken ct = default);
}

/// <summary>
/// #45 — Deployment isolation service.
/// </summary>
public interface IDeploymentManager
{
    Task<DeploymentConfig> GetConfigAsync(string tenantId, CancellationToken ct = default);
    Task ProvisionAsync(DeploymentConfig config, CancellationToken ct = default);
    Task DeprovisionAsync(string tenantId, CancellationToken ct = default);
}

/// <summary>
/// #46 — Admin console backend service.
/// </summary>
public interface IAdminConsole
{
    Task<AdminDashboard> GetDashboardAsync(CancellationToken ct = default);
    Task BroadcastAlertAsync(AdminAlert alert, CancellationToken ct = default);
}

/// <summary>
/// #47 — Explainability service.
/// </summary>
public interface IExplainabilityService
{
    Task<DecisionExplanation> ExplainDecisionAsync(string decisionId, CancellationToken ct = default);
    Task<DecisionExplanation> GenerateExplanationAsync(string agentName, string question, string answer, CancellationToken ct = default);
}

/// <summary>
/// #48 — Capability negotiation service.
/// </summary>
public interface ICapabilityNegotiator
{
    Task<AgentCapabilityCard> AnnounceCapabilitiesAsync(string agentId, CancellationToken ct = default);
    Task<IReadOnlyList<AgentCapabilityCard>> DiscoverAgentsAsync(string protocol = "A2A", CancellationToken ct = default);
}

/// <summary>
/// #49 — Agent Communication Protocol (A2A) service.
/// </summary>
public interface IAgentCommunication
{
    Task SendMessageAsync(AgentMessage message, CancellationToken ct = default);
    Task<IReadOnlyList<AgentMessage>> GetInboxAsync(string agentId, CancellationToken ct = default);
    Task<AgentMessage> WaitForReplyAsync(string messageId, TimeSpan timeout, CancellationToken ct = default);
}

/// <summary>
/// #50 — Structured Output validation service.
/// </summary>
public interface IStructuredOutputService
{
    Task<T> ParseAndValidateAsync<T>(string jsonOutput, CancellationToken ct = default);
    string GenerateJsonSchema<T>();
}
