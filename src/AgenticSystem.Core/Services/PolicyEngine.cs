using System.Collections.Concurrent;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Core.Services;

/// <summary>
/// Deny-first policy engine. Evaluates all matching policies in priority order.
/// First deny wins; if no deny, first approval-required wins; otherwise allow.
/// </summary>
public class PolicyEngine : IPolicyEngine
{
    private readonly ConcurrentDictionary<string, AgentPolicy> _policies = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger<PolicyEngine> _logger;

    public PolicyEngine(ILogger<PolicyEngine> logger)
    {
        _logger = logger;
    }

    public Task<PolicyEvaluation> EvaluateAsync(PolicyContext context, CancellationToken ct = default)
    {
        var applicable = GetApplicablePolicies(context);

        if (applicable.Count == 0)
        {
            return Task.FromResult(PolicyEvaluation.Allow("No policies defined; default allow."));
        }

        foreach (var policy in applicable)
        {
            // Check denied tools (blocklist)
            if (!string.IsNullOrWhiteSpace(context.ToolName) && policy.DeniedTools.Count > 0)
            {
                if (policy.DeniedTools.Any(denied =>
                    context.ToolName.Equals(denied, StringComparison.OrdinalIgnoreCase)))
                {
                    var violation = new PolicyViolation
                    {
                        PolicyId = policy.Id,
                        PolicyName = policy.Name,
                        Type = PolicyViolationType.ToolDenied,
                        Description = $"Tool '{context.ToolName}' is explicitly denied by policy '{policy.Name}'.",
                        ActualValue = context.ToolName,
                        AllowedValue = "Not in deny list"
                    };
                    _logger.LogWarning("Policy violation: {Description}", violation.Description);
                    return Task.FromResult(PolicyEvaluation.Deny(violation.Description, [violation], policy));
                }
            }

            // Check allowed tool categories
            if (!string.IsNullOrWhiteSpace(context.ToolCategory) && policy.AllowedToolCategories.Count > 0)
            {
                if (!policy.AllowedToolCategories.Any(cat =>
                    context.ToolCategory.Equals(cat, StringComparison.OrdinalIgnoreCase)))
                {
                    var violation = new PolicyViolation
                    {
                        PolicyId = policy.Id,
                        PolicyName = policy.Name,
                        Type = PolicyViolationType.CategoryDenied,
                        Description = $"Tool category '{context.ToolCategory}' is not allowed by policy '{policy.Name}'.",
                        ActualValue = context.ToolCategory,
                        AllowedValue = string.Join(", ", policy.AllowedToolCategories)
                    };
                    return Task.FromResult(PolicyEvaluation.Deny(violation.Description, [violation], policy));
                }
            }

            // Check allowed providers
            if (!string.IsNullOrWhiteSpace(context.Provider) && policy.AllowedProviders.Count > 0)
            {
                if (!policy.AllowedProviders.Any(p =>
                    context.Provider.Equals(p, StringComparison.OrdinalIgnoreCase)))
                {
                    var violation = new PolicyViolation
                    {
                        PolicyId = policy.Id,
                        PolicyName = policy.Name,
                        Type = PolicyViolationType.ProviderDenied,
                        Description = $"Provider '{context.Provider}' is not allowed by policy '{policy.Name}'.",
                        ActualValue = context.Provider,
                        AllowedValue = string.Join(", ", policy.AllowedProviders)
                    };
                    return Task.FromResult(PolicyEvaluation.Deny(violation.Description, [violation], policy));
                }
            }

            // Check cost limits
            if (context.EstimatedCost.HasValue && policy.MaxCostPerRequest.HasValue)
            {
                if (context.EstimatedCost.Value > policy.MaxCostPerRequest.Value)
                {
                    var violation = new PolicyViolation
                    {
                        PolicyId = policy.Id,
                        PolicyName = policy.Name,
                        Type = PolicyViolationType.BudgetExceeded,
                        Description = $"Estimated cost ${context.EstimatedCost:F4} exceeds policy limit ${policy.MaxCostPerRequest:F4}.",
                        ActualValue = context.EstimatedCost.Value.ToString("F4"),
                        AllowedValue = policy.MaxCostPerRequest.Value.ToString("F4")
                    };
                    return Task.FromResult(PolicyEvaluation.Deny(violation.Description, [violation], policy));
                }
            }

            // Check token limits
            if (context.EstimatedTokens.HasValue && policy.MaxTokensPerRequest.HasValue)
            {
                if (context.EstimatedTokens.Value > policy.MaxTokensPerRequest.Value)
                {
                    var violation = new PolicyViolation
                    {
                        PolicyId = policy.Id,
                        PolicyName = policy.Name,
                        Type = PolicyViolationType.TokenLimitExceeded,
                        Description = $"Estimated tokens {context.EstimatedTokens} exceeds policy limit {policy.MaxTokensPerRequest}.",
                        ActualValue = context.EstimatedTokens.Value.ToString(),
                        AllowedValue = policy.MaxTokensPerRequest.Value.ToString()
                    };
                    return Task.FromResult(PolicyEvaluation.Deny(violation.Description, [violation], policy));
                }
            }

            // Check autonomy level vs risk
            if (context.RiskLevel > MapAutonomyToMaxRisk(policy.MaxAutonomyLevel))
            {
                var violation = new PolicyViolation
                {
                    PolicyId = policy.Id,
                    PolicyName = policy.Name,
                    Type = PolicyViolationType.AutonomyExceeded,
                    Description = $"Risk level '{context.RiskLevel}' exceeds autonomy level '{policy.MaxAutonomyLevel}' threshold.",
                    ActualValue = context.RiskLevel.ToString(),
                    AllowedValue = policy.MaxAutonomyLevel.ToString()
                };

                // Autonomy exceeded means approval required (not hard deny)
                return Task.FromResult(PolicyEvaluation.RequireApproval(violation.Description, policy));
            }

            // Check if approval is required by policy threshold
            if (context.RiskLevel >= policy.ApprovalThreshold)
            {
                return Task.FromResult(PolicyEvaluation.RequireApproval(
                    $"Risk level '{context.RiskLevel}' meets approval threshold '{policy.ApprovalThreshold}' in policy '{policy.Name}'.",
                    policy));
            }

            // Check if final approval is required
            if (policy.RequireFinalApproval)
            {
                return Task.FromResult(PolicyEvaluation.RequireApproval(
                    $"Policy '{policy.Name}' requires final human approval for all responses.",
                    policy));
            }
        }

        var matchedPolicy = applicable[0];
        return Task.FromResult(PolicyEvaluation.Allow(
            $"All policies passed. Effective autonomy: {matchedPolicy.MaxAutonomyLevel}.",
            matchedPolicy));
    }

    public Task<IReadOnlyList<AgentPolicy>> GetPoliciesAsync(string? agentName = null, CancellationToken ct = default)
    {
        var policies = _policies.Values
            .Where(p => p.IsActive)
            .Where(p => string.IsNullOrWhiteSpace(agentName)
                || string.IsNullOrWhiteSpace(p.AgentNamePattern)
                || MatchesPattern(agentName, p.AgentNamePattern))
            .OrderByDescending(p => p.Priority)
            .ToList();

        return Task.FromResult<IReadOnlyList<AgentPolicy>>(policies);
    }

    public Task SavePolicyAsync(AgentPolicy policy, CancellationToken ct = default)
    {
        policy.UpdatedAt = DateTime.UtcNow;
        _policies[policy.Id] = policy;
        _logger.LogInformation("Policy saved: {PolicyId} ({PolicyName})", policy.Id, policy.Name);
        return Task.CompletedTask;
    }

    public Task DeletePolicyAsync(string policyId, CancellationToken ct = default)
    {
        if (_policies.TryRemove(policyId, out var removed))
        {
            _logger.LogInformation("Policy deleted: {PolicyId} ({PolicyName})", policyId, removed.Name);
        }

        return Task.CompletedTask;
    }

    private List<AgentPolicy> GetApplicablePolicies(PolicyContext context)
    {
        return _policies.Values
            .Where(p => p.IsActive)
            .Where(p => string.IsNullOrWhiteSpace(p.AgentNamePattern)
                || (!string.IsNullOrWhiteSpace(context.AgentName) && MatchesPattern(context.AgentName, p.AgentNamePattern)))
            .Where(p => string.IsNullOrWhiteSpace(p.TenantId)
                || (!string.IsNullOrWhiteSpace(context.TenantId) && p.TenantId.Equals(context.TenantId, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(p => p.Priority)
            .ToList();
    }

    private static bool MatchesPattern(string value, string pattern)
    {
        if (pattern == "*") return true;
        if (pattern.EndsWith('*'))
            return value.StartsWith(pattern[..^1], StringComparison.OrdinalIgnoreCase);
        return value.Equals(pattern, StringComparison.OrdinalIgnoreCase);
    }

    private static ToolRiskLevel MapAutonomyToMaxRisk(AutonomyLevel level) => level switch
    {
        AutonomyLevel.Manual => ToolRiskLevel.Low,
        AutonomyLevel.Assisted => ToolRiskLevel.Low,
        AutonomyLevel.Supervised => ToolRiskLevel.Medium,
        AutonomyLevel.SemiAutonomous => ToolRiskLevel.High,
        AutonomyLevel.Autonomous => ToolRiskLevel.Critical,
        AutonomyLevel.FullAutonomy => ToolRiskLevel.Critical,
        _ => ToolRiskLevel.Low
    };
}
