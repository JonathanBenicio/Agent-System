using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Interfaces;

/// <summary>
/// Enforces resource limits and isolation boundaries for tenants.
/// </summary>
public interface ITenantIsolationEnforcer
{
    /// <summary>
    /// Checks if the tenant can start a new session.
    /// </summary>
    Task<bool> CanStartSessionAsync(string tenantId, CancellationToken ct = default);

    /// <summary>
    /// Checks whether a tenant can add an agent. An existing agent can be updated at the limit.
    /// </summary>
    Task<bool> CanCreateAgentAsync(string tenantId, string agentName, CancellationToken ct = default);

    /// <summary>
    /// Checks if the tenant can ingest more documents.
    /// </summary>
    Task<bool> CanIngestDocumentAsync(string tenantId, long newDocumentSizeCount = 1, long newBytesCount = 0, CancellationToken ct = default);

    /// <summary>
    /// Returns the current usage metrics for a tenant.
    /// </summary>
    Task<TenantUsageSummary> GetUsageAsync(string tenantId, CancellationToken ct = default);
}

public class TenantUsageSummary
{
    public string TenantId { get; set; } = string.Empty;
    public int ActiveSessions { get; set; }
    public int ActiveAgents { get; set; }
    public int TotalDocuments { get; set; }
    public long StorageUsageBytes { get; set; }
    public double MonthlyBudgetSpentUsd { get; set; }
    public TenantResourceLimits Limits { get; set; } = new();
}
