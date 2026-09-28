using System.Text.Json;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Infrastructure.Persistence;

public class PostgresDynamicAgentRepository : IDynamicAgentRepository
{
    private readonly IDbContextFactory<AgenticDbContext> _dbContextFactory;
    private readonly ILogger<PostgresDynamicAgentRepository> _logger;

    public PostgresDynamicAgentRepository(
        IDbContextFactory<AgenticDbContext> dbContextFactory,
        ILogger<PostgresDynamicAgentRepository> logger)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    public async Task<IEnumerable<AgentSpecification>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entities = await dbContext.DynamicAgents
            .Where(a => a.IsActive)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToSpecification);
    }

    public async Task<AgentSpecification?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await dbContext.DynamicAgents
            .FirstOrDefaultAsync(a => a.Name == name && a.IsActive, cancellationToken);

        if (entity == null)
            return null;

        return MapToSpecification(entity);
    }

    public async Task SaveAsync(AgentSpecification specification, CancellationToken cancellationToken = default)
    {
        using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await dbContext.DynamicAgents
            .FirstOrDefaultAsync(a => a.Name == specification.Name, cancellationToken);

        if (entity == null)
        {
            entity = new DynamicAgentEntity
            {
                Name = specification.Name,
            };
            dbContext.DynamicAgents.Add(entity);
        }

        entity.Description = specification.Description;
        entity.Domain = specification.Domain;
        entity.Tier = (int)specification.Tier;
        entity.Instructions = specification.Instructions;
        entity.AutonomyLevel = (int)specification.AutonomyLevel;
        entity.AllowedToolsJson = JsonSerializer.Serialize(specification.AllowedTools);
        entity.IsActive = true;
        entity.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("✅ Dynamic agent {Agent} saved to PostgreSQL", specification.Name);
    }

    public async Task<bool> DeactivateAsync(string name, CancellationToken cancellationToken = default)
    {
        using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await dbContext.DynamicAgents
            .FirstOrDefaultAsync(a => a.Name == name && a.IsActive, cancellationToken);

        if (entity == null)
            return false;

        entity.IsActive = false;
        entity.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("🗑️ Dynamic agent {Agent} deactivated in PostgreSQL", name);
        return true;
    }

    private static AgentSpecification MapToSpecification(DynamicAgentEntity entity)
    {
        return new AgentSpecification
        {
            Name = entity.Name,
            Description = entity.Description,
            Domain = entity.Domain,
            Tier = (AgentTier)entity.Tier,
            Instructions = entity.Instructions,
            AutonomyLevel = (AutonomyLevel)entity.AutonomyLevel,
            AllowedTools = string.IsNullOrEmpty(entity.AllowedToolsJson) 
                ? new List<string>() 
                : (JsonSerializer.Deserialize<string[]>(entity.AllowedToolsJson) ?? Array.Empty<string>()).ToList()
        };
    }
}

