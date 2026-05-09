using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Interfaces;

public interface IPolicyStore
{
    Task<IReadOnlyList<AgentPolicy>> GetPoliciesAsync(string? agentName = null, CancellationToken ct = default);
    Task SavePolicyAsync(AgentPolicy policy, CancellationToken ct = default);
    Task DeletePolicyAsync(string policyId, CancellationToken ct = default);
}
