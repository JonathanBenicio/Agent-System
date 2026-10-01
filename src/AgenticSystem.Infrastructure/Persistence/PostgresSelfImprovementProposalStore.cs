using System.Text.Json;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgenticSystem.Infrastructure.Persistence;

public sealed class PostgresSelfImprovementProposalStore : ISelfImprovementProposalStore
{
    private readonly IDbContextFactory<AgenticDbContext> _dbContextFactory;

    public PostgresSelfImprovementProposalStore(IDbContextFactory<AgenticDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task SaveAsync(SelfImprovementRecord proposal, CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        db.SelfImprovementProposals.Add(ToEntity(proposal));
        await db.SaveChangesAsync(ct);
    }

    public async Task<SelfImprovementRecord?> GetAsync(string proposalId, CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var entity = await db.SelfImprovementProposals.AsNoTracking()
            .SingleOrDefaultAsync(proposal => proposal.Id == proposalId, ct);
        return entity is null ? null : ToModel(entity);
    }

    public async Task<IReadOnlyList<SelfImprovementRecord>> GetAllAsync(CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var proposals = await db.SelfImprovementProposals.AsNoTracking()
            .OrderByDescending(proposal => proposal.CreatedAt)
            .ToListAsync(ct);
        return proposals.Select(ToModel).ToList();
    }

    public async Task UpdateAsync(SelfImprovementRecord proposal, CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var entity = await db.SelfImprovementProposals
            .SingleOrDefaultAsync(item => item.Id == proposal.Id, ct)
            ?? throw new KeyNotFoundException("Self-improvement proposal was not found in the active tenant.");

        entity.Status = proposal.Status;
        entity.ReviewedBy = proposal.ReviewedBy;
        entity.ReviewedAt = proposal.ReviewedAt;
        entity.AppliedPromptVersion = proposal.AppliedPromptVersion;
        entity.AppliedAgentVersionId = proposal.AppliedAgentVersionId;
        await db.SaveChangesAsync(ct);
    }

    private static SelfImprovementProposalEntity ToEntity(SelfImprovementRecord proposal) => new()
    {
        Id = proposal.Id,
        TenantId = proposal.TenantId,
        AgentName = proposal.AgentName,
        Type = proposal.Type.ToString(),
        Status = proposal.Status,
        ConfidenceLevel = proposal.ConfidenceLevel,
        Rationale = proposal.Rationale ?? string.Empty,
        ProposedChangesJson = JsonSerializer.Serialize(proposal.ProposedChanges),
        PreviousInstructions = proposal.PreviousInstructions,
        CreatedBy = proposal.CreatedBy,
        ReviewedBy = proposal.ReviewedBy,
        CreatedAt = proposal.LearnedAt,
        ReviewedAt = proposal.ReviewedAt,
        AppliedPromptVersion = proposal.AppliedPromptVersion,
        AppliedAgentVersionId = proposal.AppliedAgentVersionId
    };

    private static SelfImprovementRecord ToModel(SelfImprovementProposalEntity entity) => new()
    {
        Id = entity.Id,
        TenantId = entity.TenantId,
        AgentName = entity.AgentName,
        Type = Enum.TryParse<ImprovementType>(entity.Type, out var type) ? type : ImprovementType.PromptRefinement,
        Status = entity.Status,
        ConfidenceLevel = entity.ConfidenceLevel,
        Rationale = entity.Rationale,
        ProposedChanges = JsonSerializer.Deserialize<Dictionary<string, string>>(entity.ProposedChangesJson) ?? new(),
        PreviousInstructions = entity.PreviousInstructions,
        CreatedBy = entity.CreatedBy,
        ReviewedBy = entity.ReviewedBy,
        LearnedAt = entity.CreatedAt,
        ReviewedAt = entity.ReviewedAt,
        AppliedPromptVersion = entity.AppliedPromptVersion,
        AppliedAgentVersionId = entity.AppliedAgentVersionId
    };
}
