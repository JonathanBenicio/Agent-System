using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Infrastructure.Persistence;

public class PostgresAgentVersionStore : IAgentVersionStore
{
    private readonly IDbContextFactory<AgenticDbContext> _dbContextFactory;
    private readonly ILogger<PostgresAgentVersionStore> _logger;

    public PostgresAgentVersionStore(IDbContextFactory<AgenticDbContext> dbContextFactory, ILogger<PostgresAgentVersionStore> logger)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    public async Task SaveAsync(AgentVersion version, CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var entity = AgentVersionEntity.FromModel(version);

        var existing = await db.AgentVersions.FindAsync(new object[] { entity.Id }, ct);
        if (existing is not null)
        {
            db.Entry(existing).CurrentValues.SetValues(entity);
            existing.Tools = entity.Tools;
        }
        else
        {
            db.AgentVersions.Add(entity);
        }

        await db.SaveChangesAsync(ct);
        _logger.LogInformation("Agent version saved to database: {VersionId} for agent {AgentName}", version.Id, version.AgentName);
    }

    public async Task<AgentVersion?> GetByIdAsync(string versionId, CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var entity = await db.AgentVersions
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == versionId, ct);

        return entity?.ToModel();
    }

    public async Task<AgentVersion?> GetActiveAsync(string agentName, AgentVersionEnvironment environment, CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var entity = await db.AgentVersions
            .AsNoTracking()
            .Where(v => v.AgentName == agentName && v.Environment == environment && v.Status == AgentVersionStatus.Active)
            .OrderByDescending(v => v.VersionNumber)
            .FirstOrDefaultAsync(ct);

        return entity?.ToModel();
    }

    public async Task<IReadOnlyList<AgentVersion>> GetHistoryAsync(string agentName, int limit = 20, CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var entities = await db.AgentVersions
            .AsNoTracking()
            .Where(v => v.AgentName == agentName)
            .OrderByDescending(v => v.VersionNumber)
            .Take(limit)
            .ToListAsync(ct);

        return entities.Select(v => v.ToModel()).ToList();
    }

    public async Task<int> GetNextVersionNumberAsync(string agentName, CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var maxVersion = await db.AgentVersions
            .AsNoTracking()
            .Where(v => v.AgentName == agentName)
            .Select(v => (int?)v.VersionNumber)
            .MaxAsync(ct);

        return (maxVersion ?? 0) + 1;
    }
}
