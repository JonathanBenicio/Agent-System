using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Infrastructure.Persistence;

public class PostgresGoldenSetRepository : IGoldenSetRepository
{
    private readonly IDbContextFactory<AgenticDbContext> _dbContextFactory;
    private readonly ILogger<PostgresGoldenSetRepository> _logger;

    public PostgresGoldenSetRepository(
        IDbContextFactory<AgenticDbContext> dbContextFactory,
        ILogger<PostgresGoldenSetRepository> logger)
    {
        _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<GoldenSet?> GetByIdAsync(string id, string tenantId, CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var entity = await db.GoldenSets
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId, ct);
        return entity?.ToModel();
    }

    public async Task<IReadOnlyList<GoldenSet>> ListAsync(string tenantId, string? agentName, int page, int pageSize, CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var query = db.GoldenSets
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(agentName))
        {
            query = query.Where(x => x.AgentName == agentName);
        }

        var entities = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return entities.Select(e => e.ToModel()).ToList();
    }

    public async Task AddAsync(GoldenSet model, CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var entity = GoldenSetEntity.FromModel(model);
        db.GoldenSets.Add(entity);
        await db.SaveChangesAsync(ct);
        _logger.LogInformation("Added Golden Set {Name} (ID: {Id}) for tenant {TenantId}", entity.Name, entity.Id, entity.TenantId);
    }

    public async Task UpdateAsync(GoldenSet model, CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        model.UpdatedAt = DateTime.UtcNow;
        var entity = GoldenSetEntity.FromModel(model);
        db.Entry(entity).State = EntityState.Modified;
        await db.SaveChangesAsync(ct);
        _logger.LogInformation("Updated Golden Set {Name} (ID: {Id}) for tenant {TenantId}", entity.Name, entity.Id, entity.TenantId);
    }

    public async Task DeleteAsync(string id, string tenantId, CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var entity = await db.GoldenSets
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId, ct);

        if (entity != null)
        {
            db.GoldenSets.Remove(entity);
            await db.SaveChangesAsync(ct);
            _logger.LogInformation("Deleted Golden Set ID: {Id} for tenant {TenantId}", id, tenantId);
        }
    }
}
