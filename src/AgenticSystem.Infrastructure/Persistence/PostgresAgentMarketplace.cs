using System.Text.Json;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Infrastructure.Persistence;

public class PostgresAgentMarketplace : IAgentMarketplace
{
    private readonly IDbContextFactory<AgenticDbContext> _dbContextFactory;
    private readonly ILogger<PostgresAgentMarketplace> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public PostgresAgentMarketplace(
        IDbContextFactory<AgenticDbContext> dbContextFactory,
        ILogger<PostgresAgentMarketplace> logger)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    public async Task<AgentMarketplaceEntry> PublishAsync(AgentSpecification spec, string author, CancellationToken ct = default)
    {
        _logger.LogInformation("🚀 Publishing agent to marketplace: {Name} by {Author}", spec.Name, author);
        
        using var context = await _dbContextFactory.CreateDbContextAsync(ct);
        
        var entry = new AgentMarketplaceEntry
        {
            Name = spec.Name,
            Domain = spec.Domain,
            Description = spec.Description,
            Tier = spec.Tier,
            AuthorName = author,
            Tags = spec.Capabilities,
            Tools = spec.AllowedTools,
            SystemPrompt = spec.Instructions,
            Status = MarketplaceEntryStatus.Published
        };

        var entity = new AgentMarketplaceEntryEntity
        {
            Id = entry.Id,
            Name = entry.Name,
            Domain = entry.Domain,
            Description = entry.Description,
            Author = entry.AuthorName ?? "Unknown",
            TagsJson = JsonSerializer.Serialize(entry.Tags, JsonOptions),
            AverageRating = 0,
            InstallCount = 0,
            SpecificationJson = JsonSerializer.Serialize(spec, JsonOptions),
            PublishedAt = entry.PublishedAt
        };

        context.AgentMarketplaceEntries.Add(entity);
        await context.SaveChangesAsync(ct);
        
        return entry;
    }

    public async Task<IReadOnlyList<AgentMarketplaceEntry>> SearchAsync(string? query = null, string? domain = null, List<string>? tags = null, CancellationToken ct = default)
    {
        using var context = await _dbContextFactory.CreateDbContextAsync(ct);
        var dbQuery = context.AgentMarketplaceEntries.AsQueryable();

        if (!string.IsNullOrEmpty(query))
        {
            dbQuery = dbQuery.Where(e => EF.Functions.ILike(e.Name, $"%{query}%") || EF.Functions.ILike(e.Description, $"%{query}%"));
        }

        if (!string.IsNullOrEmpty(domain))
        {
            dbQuery = dbQuery.Where(e => e.Domain == domain);
        }

        var entities = await dbQuery.OrderByDescending(e => e.PublishedAt).ToListAsync(ct);
        
        return entities.Select(Map).ToList();
    }

    public async Task<AgentMarketplaceEntry?> GetAsync(string entryId, CancellationToken ct = default)
    {
        using var context = await _dbContextFactory.CreateDbContextAsync(ct);
        var entity = await context.AgentMarketplaceEntries.FindAsync(new object[] { entryId }, ct);
        return entity == null ? null : Map(entity);
    }

    public async Task RateAgentAsync(string entryId, int rating, string? comment = null, CancellationToken ct = default)
    {
        using var context = await _dbContextFactory.CreateDbContextAsync(ct);
        var entity = await context.AgentMarketplaceEntries.FindAsync(new object[] { entryId }, ct);
        if (entity != null)
        {
            entity.AverageRating = (entity.AverageRating * entity.InstallCount + rating) / (entity.InstallCount + 1);
            entity.InstallCount++;
            await context.SaveChangesAsync(ct);
        }
    }

    private static AgentMarketplaceEntry Map(AgentMarketplaceEntryEntity entity)
    {
        var spec = JsonSerializer.Deserialize<AgentSpecification>(entity.SpecificationJson, JsonOptions) ?? new();
        return new AgentMarketplaceEntry
        {
            Id = entity.Id,
            Name = entity.Name,
            Domain = entity.Domain,
            Description = entity.Description,
            Tier = spec.Tier,
            AuthorName = entity.Author,
            Tags = JsonSerializer.Deserialize<List<string>>(entity.TagsJson, JsonOptions) ?? new(),
            Tools = spec.AllowedTools,
            SystemPrompt = spec.Instructions,
            Rating = entity.AverageRating,
            InstallCount = entity.InstallCount,
            PublishedAt = entity.PublishedAt
        };
    }
}
