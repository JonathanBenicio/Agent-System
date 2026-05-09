using System.Text.Json;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Infrastructure.Persistence;

public class PostgresMemoryLifecycleStore : IMemoryLifecycleStore
{
    private readonly IDbContextFactory<AgenticDbContext> _dbContextFactory;
    private readonly ILogger<PostgresMemoryLifecycleStore> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public PostgresMemoryLifecycleStore(
        IDbContextFactory<AgenticDbContext> dbContextFactory,
        ILogger<PostgresMemoryLifecycleStore> logger)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    public async Task SaveAsync(EnhancedMemoryEntry entry, CancellationToken ct = default)
    {
        using var context = await _dbContextFactory.CreateDbContextAsync(ct);
        var entity = await context.EnhancedMemories.FindAsync(new object[] { entry.Id }, ct);

        if (entity == null)
        {
            entity = new EnhancedMemoryEntity { Id = entry.Id };
            context.EnhancedMemories.Add(entity);
        }

        entity.AgentName = entry.AgentName;
        entity.SessionId = entry.SessionId;
        entity.Content = entry.Content;
        entity.MemoryType = entry.MemoryType.ToString();
        entity.Sensitivity = entry.Sensitivity.ToString();
        entity.Confidence = entry.Confidence;
        entity.Freshness = entry.Freshness;
        entity.DecayRate = entry.DecayRate;
        entity.AccessCount = entry.AccessCount;
        entity.CreatedAt = entry.CreatedAt;
        entity.LastAccessedAt = entry.LastAccessedAt;
        entity.ExpiresAt = entry.ExpiresAt;
        entity.TagsJson = JsonSerializer.Serialize(entry.Tags, JsonOptions);

        await context.SaveChangesAsync(ct);
    }

    public async Task<EnhancedMemoryEntry?> GetAsync(string id, CancellationToken ct = default)
    {
        using var context = await _dbContextFactory.CreateDbContextAsync(ct);
        var entity = await context.EnhancedMemories.FindAsync(new object[] { id }, ct);
        return entity == null ? null : Map(entity);
    }

    public async Task<IReadOnlyList<EnhancedMemoryEntry>> ListAsync(string? sessionId = null, string? agentName = null, CancellationToken ct = default)
    {
        using var context = await _dbContextFactory.CreateDbContextAsync(ct);
        var query = context.EnhancedMemories.AsQueryable();

        if (!string.IsNullOrEmpty(sessionId)) query = query.Where(e => e.SessionId == sessionId);
        if (!string.IsNullOrEmpty(agentName)) query = query.Where(e => e.AgentName == agentName);

        var entities = await query.ToListAsync(ct);
        return entities.Select(Map).ToList();
    }

    public async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        using var context = await _dbContextFactory.CreateDbContextAsync(ct);
        var entity = await context.EnhancedMemories.FindAsync(new object[] { id }, ct);
        if (entity != null)
        {
            context.EnhancedMemories.Remove(entity);
            await context.SaveChangesAsync(ct);
        }
    }

    private static EnhancedMemoryEntry Map(EnhancedMemoryEntity entity)
    {
        return new EnhancedMemoryEntry
        {
            Id = entity.Id,
            AgentName = entity.AgentName,
            SessionId = entity.SessionId,
            Content = entity.Content,
            MemoryType = Enum.Parse<MemoryType>(entity.MemoryType),
            Sensitivity = Enum.Parse<MemorySensitivity>(entity.Sensitivity),
            Confidence = entity.Confidence,
            Freshness = entity.Freshness,
            DecayRate = entity.DecayRate,
            AccessCount = entity.AccessCount,
            CreatedAt = entity.CreatedAt,
            LastAccessedAt = entity.LastAccessedAt,
            ExpiresAt = entity.ExpiresAt,
            Tags = JsonSerializer.Deserialize<Dictionary<string, string>>(entity.TagsJson, JsonOptions) ?? new()
        };
    }
}
