using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Interfaces;

/// <summary>
/// Declarative policy engine — evaluates agent policies in deny-first chain.
/// Integrates with TenantLimits and ToolGovernanceService for enforcement.
/// </summary>
public interface IPolicyEngine
{
    /// <summary>
    /// Evaluates all applicable policies for a given context.
    /// Returns deny on first violation (deny-first chain).
    /// </summary>
    Task<PolicyEvaluation> EvaluateAsync(PolicyContext context, CancellationToken ct = default);

    /// <summary>
    /// Gets all policies, optionally filtered by agent name.
    /// </summary>
    Task<IReadOnlyList<AgentPolicy>> GetPoliciesAsync(string? agentName = null, CancellationToken ct = default);

    /// <summary>
    /// Creates or updates a policy.
    /// </summary>
    Task SavePolicyAsync(AgentPolicy policy, CancellationToken ct = default);

    /// <summary>
    /// Deletes a policy by ID.
    /// </summary>
    Task DeletePolicyAsync(string policyId, CancellationToken ct = default);
}
