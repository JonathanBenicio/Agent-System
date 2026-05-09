using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Infrastructure.Persistence;

public class PostgresEvalResultStore : IEvalResultStore
{
    private readonly IDbContextFactory<AgenticDbContext> _dbContextFactory;
    private readonly ILogger<PostgresEvalResultStore> _logger;

    public PostgresEvalResultStore(IDbContextFactory<AgenticDbContext> dbContextFactory, ILogger<PostgresEvalResultStore> logger)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    public async Task SaveSuiteResultAsync(EvalSuiteResult result, CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var entity = EvalSuiteResultEntity.FromModel(result);

        var existing = await db.EvalSuiteResults.FindAsync(new object[] { entity.SuiteId }, ct);
        if (existing is not null)
        {
            db.Entry(existing).CurrentValues.SetValues(entity);
        }
        else
        {
            db.EvalSuiteResults.Add(entity);
        }

        await db.SaveChangesAsync(ct);
        _logger.LogInformation("Evaluation suite result saved to database: {SuiteId} for agent {AgentName}", result.SuiteId, result.AgentName);
    }

    public async Task<EvalSuiteResult?> GetLatestBaselineAsync(string agentName, CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var entity = await db.EvalSuiteResults
            .AsNoTracking()
            .Where(e => e.AgentName == agentName)
            .OrderByDescending(e => e.StartedAt)
            .FirstOrDefaultAsync(ct);

        return entity?.ToModel();
    }

    public async Task<IReadOnlyList<EvalSuiteResult>> GetHistoryAsync(string agentName, int limit = 10, CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var entities = await db.EvalSuiteResults
            .AsNoTracking()
            .Where(e => e.AgentName == agentName)
            .OrderByDescending(e => e.StartedAt)
            .Take(limit)
            .ToListAsync(ct);

        return entities.Select(e => e.ToModel()).ToList();
    }
}
