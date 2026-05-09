using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Infrastructure.Persistence;

public class PostgresPromptTemplateStore : IPromptTemplateStore
{
    private readonly IDbContextFactory<AgenticDbContext> _dbContextFactory;
    private readonly ILogger<PostgresPromptTemplateStore> _logger;

    public PostgresPromptTemplateStore(IDbContextFactory<AgenticDbContext> dbContextFactory, ILogger<PostgresPromptTemplateStore> logger)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    public async Task SaveAsync(PromptTemplate template, CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var entity = PromptTemplateEntity.FromModel(template);

        var existing = await db.PromptTemplates.FindAsync(new object[] { entity.Id }, ct);
        if (existing is not null)
        {
            db.Entry(existing).CurrentValues.SetValues(entity);
            existing.Variables = entity.Variables;
        }
        else
        {
            db.PromptTemplates.Add(entity);
        }

        await db.SaveChangesAsync(ct);
        _logger.LogInformation("Prompt template saved to database: {TemplateId} ({Name})", template.Id, template.Name);
    }

    public async Task<PromptTemplate?> GetActiveAsync(string agentName, string locale, CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var entity = await db.PromptTemplates
            .AsNoTracking()
            .Where(p => p.AgentName == agentName && p.Locale == locale && p.IsActive)
            .OrderByDescending(p => p.Version)
            .FirstOrDefaultAsync(ct);

        return entity?.ToModel();
    }

    public async Task<IReadOnlyList<PromptTemplate>> GetAllForAgentAsync(string agentName, CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var entities = await db.PromptTemplates
            .AsNoTracking()
            .Where(p => p.AgentName == agentName)
            .OrderByDescending(p => p.Version)
            .ToListAsync(ct);

        return entities.Select(p => p.ToModel()).ToList();
    }
}
