using System.Collections.Concurrent;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Services;

public sealed class InMemorySelfImprovementProposalStore : ISelfImprovementProposalStore
{
    private readonly ConcurrentDictionary<(string TenantId, string Id), SelfImprovementRecord> _proposals = new();
    private readonly ITenantContextAccessor _tenantContextAccessor;

    public InMemorySelfImprovementProposalStore(ITenantContextAccessor tenantContextAccessor)
    {
        _tenantContextAccessor = tenantContextAccessor;
    }

    public Task SaveAsync(SelfImprovementRecord proposal, CancellationToken ct = default)
    {
        var tenantId = _tenantContextAccessor.CurrentTenantId;
        proposal.TenantId = tenantId;
        _proposals[(tenantId, proposal.Id)] = proposal;
        return Task.CompletedTask;
    }

    public Task<SelfImprovementRecord?> GetAsync(string proposalId, CancellationToken ct = default)
    {
        var key = (_tenantContextAccessor.CurrentTenantId, proposalId);
        _proposals.TryGetValue(key, out var proposal);
        return Task.FromResult(proposal);
    }

    public Task<IReadOnlyList<SelfImprovementRecord>> GetAllAsync(CancellationToken ct = default)
    {
        var tenantId = _tenantContextAccessor.CurrentTenantId;
        IReadOnlyList<SelfImprovementRecord> proposals = _proposals.Values
            .Where(proposal => proposal.TenantId == tenantId)
            .OrderByDescending(proposal => proposal.LearnedAt)
            .ToList();
        return Task.FromResult(proposals);
    }

    public Task UpdateAsync(SelfImprovementRecord proposal, CancellationToken ct = default)
    {
        var key = (_tenantContextAccessor.CurrentTenantId, proposal.Id);
        if (!_proposals.ContainsKey(key))
            throw new KeyNotFoundException("Self-improvement proposal was not found in the active tenant.");
        _proposals[key] = proposal;
        return Task.CompletedTask;
    }
}
