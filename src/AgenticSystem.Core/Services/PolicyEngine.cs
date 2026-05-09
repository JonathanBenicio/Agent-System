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
    private readonly IPolicyStore _policyStore;
    private readonly ILogger<PolicyEngine> _logger;

    public PolicyEngine(IPolicyStore policyStore, ILogger<PolicyEngine> logger)
    {
        _policyStore = policyStore;
        _logger = logger;
    }

    public async Task<PolicyEvaluation> EvaluateAsync(PolicyContext context, CancellationToken ct = default)
    {
        var applicable = await GetApplicablePoliciesAsync(context, ct);

        if (applicable.Count == 0)
        {
            return PolicyEvaluation.Allow("No policies defined; default allow.");
        }

        foreach (var policy in applicable)
        {
            // 1. Check Content Filters (Prompt Injection, PII, etc.)
            if (policy.ContentFilters.Count > 0)
            {
                var input = context.Metadata.GetValueOrDefault("UserInput")?.ToString() ?? string.Empty;
                foreach (var filter in policy.ContentFilters)
                {
                    if (filter.Equals("no-pii", StringComparison.OrdinalIgnoreCase) && ContainsPii(input))
                    {
                        var violation = new PolicyViolation
                        {
                            PolicyId = policy.Id,
                            PolicyName = policy.Name,
                            Type = PolicyViolationType.ContentFilterViolation,
                            Description = "PII detected in user input, blocked by policy.",
                            ActualValue = "PII Data",
                            AllowedValue = "no-pii"
                        };
                        return PolicyEvaluation.Deny(violation.Description, [violation], policy);
                    }
                    if (filter.Equals("no-prompt-injection", StringComparison.OrdinalIgnoreCase) && DetectsPromptInjection(input))
                    {
                        var violation = new PolicyViolation
                        {
                            PolicyId = policy.Id,
                            PolicyName = policy.Name,
                            Type = PolicyViolationType.ContentFilterViolation,
                            Description = "Potential prompt injection detected.",
                            ActualValue = "Injection Pattern",
                            AllowedValue = "no-prompt-injection"
                        };
                        return PolicyEvaluation.Deny(violation.Description, [violation], policy);
                    }
                }
            }
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
                    return PolicyEvaluation.Deny(violation.Description, [violation], policy);
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
                    return PolicyEvaluation.Deny(violation.Description, [violation], policy);
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
                    return PolicyEvaluation.Deny(violation.Description, [violation], policy);
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
                    return PolicyEvaluation.Deny(violation.Description, [violation], policy);
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
                    return PolicyEvaluation.Deny(violation.Description, [violation], policy);
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
                return PolicyEvaluation.RequireApproval(violation.Description, policy);
            }

            // Check if approval is required by policy threshold
            if (context.RiskLevel >= policy.ApprovalThreshold)
            {
                return PolicyEvaluation.RequireApproval(
                    $"Risk level '{context.RiskLevel}' meets approval threshold '{policy.ApprovalThreshold}' in policy '{policy.Name}'.",
                    policy);
            }

            // Check if final approval is required
            if (policy.RequireFinalApproval)
            {
                return PolicyEvaluation.RequireApproval(
                    $"Policy '{policy.Name}' requires final human approval for all responses.",
                    policy);
            }
        }

        var matchedPolicy = applicable[0];
        return PolicyEvaluation.Allow(
            $"All policies passed. Effective autonomy: {matchedPolicy.MaxAutonomyLevel}.",
            matchedPolicy);
    }

    public Task<IReadOnlyList<AgentPolicy>> GetPoliciesAsync(string? agentName = null, CancellationToken ct = default)
    {
        return _policyStore.GetPoliciesAsync(agentName, ct);
    }

    public Task SavePolicyAsync(AgentPolicy policy, CancellationToken ct = default)
    {
        return _policyStore.SavePolicyAsync(policy, ct);
    }

    public Task DeletePolicyAsync(string policyId, CancellationToken ct = default)
    {
        return _policyStore.DeletePolicyAsync(policyId, ct);
    }

    private async Task<List<AgentPolicy>> GetApplicablePoliciesAsync(PolicyContext context, CancellationToken ct)
    {
        var policies = await _policyStore.GetPoliciesAsync(context.AgentName, ct);

        return policies
            .Where(p => string.IsNullOrWhiteSpace(p.TenantId)
                || (!string.IsNullOrWhiteSpace(context.TenantId) && p.TenantId.Equals(context.TenantId, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(p => p.Priority)
            .ToList();
    }

    private static bool ContainsPii(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        // Basic heuristic for PII (Credit cards, SSN patterns)
        return System.Text.RegularExpressions.Regex.IsMatch(text, @"\b\d{3}-\d{2}-\d{4}\b") || 
               System.Text.RegularExpressions.Regex.IsMatch(text, @"\b(?:\d[ -]*?){13,16}\b");
    }

    private static bool DetectsPromptInjection(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        var lower = text.ToLowerInvariant();
        return lower.Contains("ignore all previous") || 
               lower.Contains("ignore previous instructions") ||
               lower.Contains("you are now") ||
               lower.Contains("forget everything");
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
