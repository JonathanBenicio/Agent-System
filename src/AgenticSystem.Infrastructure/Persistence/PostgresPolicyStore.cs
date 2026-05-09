using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Infrastructure.Persistence;

public class PostgresPolicyStore : IPolicyStore
{
    private readonly IDbContextFactory<AgenticDbContext> _dbContextFactory;
    private readonly ILogger<PostgresPolicyStore> _logger;

    public PostgresPolicyStore(IDbContextFactory<AgenticDbContext> dbContextFactory, ILogger<PostgresPolicyStore> logger)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    public async Task<IReadOnlyList<AgentPolicy>> GetPoliciesAsync(string? agentName = null, CancellationToken ct = default)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync(ct);

        var query = context.AgentPolicies.AsNoTracking().Where(p => p.IsActive);

        var entities = await query.ToListAsync(ct);

        return entities
            .Where(p => string.IsNullOrWhiteSpace(agentName) 
                     || string.IsNullOrWhiteSpace(p.AgentNamePattern) 
                     || MatchesPattern(agentName, p.AgentNamePattern))
            .OrderByDescending(p => p.Priority)
            .Select(p => p.ToModel())
            .ToList();
    }

    public async Task SavePolicyAsync(AgentPolicy policy, CancellationToken ct = default)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync(ct);
        
        var entity = AgentPolicyEntity.FromModel(policy);
        entity.UpdatedAt = DateTime.UtcNow;

        var existing = await context.AgentPolicies.FindAsync(new object[] { entity.Id }, ct);
        if (existing != null)
        {
            context.Entry(existing).CurrentValues.SetValues(entity);
            existing.AllowedToolCategories = entity.AllowedToolCategories;
            existing.DeniedTools = entity.DeniedTools;
            existing.AllowedProviders = entity.AllowedProviders;
            existing.ContentFilters = entity.ContentFilters;
        }
        else
        {
            await context.AgentPolicies.AddAsync(entity, ct);
        }

        await context.SaveChangesAsync(ct);
        _logger.LogInformation("Policy saved to database: {PolicyId} ({PolicyName})", policy.Id, policy.Name);
    }

    public async Task DeletePolicyAsync(string policyId, CancellationToken ct = default)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync(ct);
        
        var entity = await context.AgentPolicies.FindAsync(new object[] { policyId }, ct);
        if (entity != null)
        {
            context.AgentPolicies.Remove(entity);
            await context.SaveChangesAsync(ct);
            _logger.LogInformation("Policy deleted from database: {PolicyId} ({PolicyName})", policyId, entity.Name);
        }
    }

    private static bool MatchesPattern(string value, string pattern)
    {
        if (pattern == "*") return true;
        if (pattern.EndsWith('*'))
            return value.StartsWith(pattern[..^1], StringComparison.OrdinalIgnoreCase);
        return value.Equals(pattern, StringComparison.OrdinalIgnoreCase);
    }
}
