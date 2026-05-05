using System.Text.Json;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Infrastructure.Persistence;

public class EfAgentMemoryStore : IAgentMemoryStore
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EfAgentMemoryStore> _logger;

    public EfAgentMemoryStore(IServiceScopeFactory scopeFactory, ILogger<EfAgentMemoryStore> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<AgentMemoryEntry> SaveAsync(AgentMemoryEntry entry, CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AgenticDbContext>();
        var existing = await db.AgentMemories.FirstOrDefaultAsync(memory => memory.Id == entry.Id, ct);

        if (existing is null)
        {
            db.AgentMemories.Add(MapToEntity(entry));
        }
        else
        {
            existing.UserId = entry.UserId;
            existing.AgentName = entry.AgentName;
            existing.SessionId = entry.SessionId;
            existing.MemoryType = entry.MemoryType.ToString();
            existing.Content = entry.Content;
            existing.Confidence = entry.Confidence;
            existing.Source = entry.Source;
            existing.KeywordsJson = JsonSerializer.Serialize(entry.Keywords);
            existing.MetadataJson = JsonSerializer.Serialize(entry.Metadata);
            existing.CreatedAt = entry.CreatedAt;
            existing.LastUsedAt = entry.LastUsedAt;
            existing.UsageCount = entry.UsageCount;
            existing.IsActive = entry.IsActive;
        }

        await db.SaveChangesAsync(ct);
        return entry;
    }

    public async Task<IReadOnlyList<AgentMemoryEntry>> GetByAgentAsync(string userId, string agentName, CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AgenticDbContext>();

        var entities = await db.AgentMemories
            .AsNoTracking()
            .Where(memory => memory.IsActive)
            .Where(memory => memory.UserId == userId)
            .Where(memory => memory.AgentName == agentName)
            .OrderByDescending(memory => memory.LastUsedAt)
            .ThenByDescending(memory => memory.CreatedAt)
            .ToListAsync(ct);

        return entities.Select(MapToModel).ToList();
    }

    private static AgentMemoryEntity MapToEntity(AgentMemoryEntry entry) => new()
    {
        Id = entry.Id,
        UserId = entry.UserId,
        AgentName = entry.AgentName,
        SessionId = entry.SessionId,
        MemoryType = entry.MemoryType.ToString(),
        Content = entry.Content,
        Confidence = entry.Confidence,
        Source = entry.Source,
        KeywordsJson = JsonSerializer.Serialize(entry.Keywords),
        MetadataJson = JsonSerializer.Serialize(entry.Metadata),
        CreatedAt = entry.CreatedAt,
        LastUsedAt = entry.LastUsedAt,
        UsageCount = entry.UsageCount,
        IsActive = entry.IsActive
    };

    private static AgentMemoryEntry MapToModel(AgentMemoryEntity entity) => new()
    {
        Id = entity.Id,
        UserId = entity.UserId,
        AgentName = entity.AgentName,
        SessionId = entity.SessionId,
        MemoryType = Enum.TryParse<AgentMemoryType>(entity.MemoryType, out var memoryType)
            ? memoryType
            : AgentMemoryType.Fact,
        Content = entity.Content,
        Confidence = entity.Confidence,
        Source = entity.Source,
        Keywords = JsonSerializer.Deserialize<List<string>>(entity.KeywordsJson) ?? new(),
        Metadata = JsonSerializer.Deserialize<Dictionary<string, string>>(entity.MetadataJson) ?? new(),
        CreatedAt = entity.CreatedAt,
        LastUsedAt = entity.LastUsedAt,
        UsageCount = entity.UsageCount,
        IsActive = entity.IsActive
    };
}