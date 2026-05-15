using System.Collections.Concurrent;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Services;

public class InMemoryPolicyStore : IPolicyStore
{
    private readonly ConcurrentDictionary<string, AgentPolicy> _policies = new();

    public Task<IReadOnlyList<AgentPolicy>> GetPoliciesAsync(string? agentName = null, CancellationToken ct = default)
    {
        var list = _policies.Values.ToList();
        
        if (!string.IsNullOrWhiteSpace(agentName))
        {
            list = list.Where(p => string.IsNullOrWhiteSpace(p.AgentNamePattern) || MatchesPattern(agentName, p.AgentNamePattern)).ToList();
        }

        return Task.FromResult<IReadOnlyList<AgentPolicy>>(list.OrderByDescending(p => p.Priority).ToList());
    }

    public Task SavePolicyAsync(AgentPolicy policy, CancellationToken ct = default)
    {
        _policies[policy.Id] = policy;
        return Task.CompletedTask;
    }

    public Task DeletePolicyAsync(string policyId, CancellationToken ct = default)
    {
        _policies.TryRemove(policyId, out _);
        return Task.CompletedTask;
    }

    private static bool MatchesPattern(string value, string pattern)
    {
        if (pattern == "*") return true;
        if (pattern.EndsWith('*'))
            return value.StartsWith(pattern[..^1], StringComparison.OrdinalIgnoreCase);
        return value.Equals(pattern, StringComparison.OrdinalIgnoreCase);
    }
}
