using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgenticSystem.Infrastructure.Persistence;

public class PostgresKnowledgeRoomStore : IKnowledgeRoomService
{
    private readonly AgenticDbContext _dbContext;

    public PostgresKnowledgeRoomStore(AgenticDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IEnumerable<KnowledgeRoom>> ListRoomsAsync(string tenantId, CancellationToken ct = default)
    {
        var entities = await _dbContext.Set<KnowledgeRoomEntity>()
            .Where(r => r.TenantId == tenantId)
            .OrderByDescending(r => r.UpdatedAt)
            .ToListAsync(ct);

        return entities.Select(MapToModel);
    }

    public async Task<KnowledgeRoom?> GetRoomAsync(string id, string tenantId, CancellationToken ct = default)
    {
        var entity = await _dbContext.Set<KnowledgeRoomEntity>()
            .FirstOrDefaultAsync(r => r.Id == id && r.TenantId == tenantId, ct);

        return entity != null ? MapToModel(entity) : null;
    }

    public async Task<KnowledgeRoom> CreateRoomAsync(string tenantId, KnowledgeRoom room, CancellationToken ct = default)
    {
        var entity = MapToEntity(room);
        entity.TenantId = tenantId;
        
        if (string.IsNullOrEmpty(entity.Id))
        {
            entity.Id = Guid.NewGuid().ToString();
        }
        
        entity.CreatedAt = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;
        
        _dbContext.Set<KnowledgeRoomEntity>().Add(entity);
        await _dbContext.SaveChangesAsync(ct);
        
        return MapToModel(entity);
    }

    public async Task<KnowledgeRoom> UpdateRoomAsync(string tenantId, KnowledgeRoom room, CancellationToken ct = default)
    {
        var entity = await _dbContext.Set<KnowledgeRoomEntity>()
            .FirstOrDefaultAsync(r => r.Id == room.Id && r.TenantId == tenantId, ct);
            
        if (entity == null)
        {
            throw new KeyNotFoundException($"Room {room.Id} not found for tenant {tenantId}");
        }
        
        entity.Name = room.Name;
        entity.Description = room.Description ?? string.Empty;
        entity.Color = room.Color;
        entity.Icon = room.Icon;
        entity.DocumentCount = room.DocumentCount;
        entity.Tags = room.Tags;
        entity.UpdatedAt = DateTime.UtcNow;
        
        await _dbContext.SaveChangesAsync(ct);
        
        return MapToModel(entity);
    }

    public async Task<bool> DeleteRoomAsync(string id, string tenantId, CancellationToken ct = default)
    {
        var entity = await _dbContext.Set<KnowledgeRoomEntity>()
            .FirstOrDefaultAsync(r => r.Id == id && r.TenantId == tenantId, ct);
            
        if (entity == null) return false;
        
        _dbContext.Set<KnowledgeRoomEntity>().Remove(entity);
        await _dbContext.SaveChangesAsync(ct);
        
        return true;
    }

    private static KnowledgeRoom MapToModel(KnowledgeRoomEntity entity)
    {
        return new KnowledgeRoom
        {
            Id = entity.Id,
            Name = entity.Name,
            Description = entity.Description,
            Color = entity.Color,
            Icon = entity.Icon,
            DocumentCount = entity.DocumentCount,
            Tags = entity.Tags,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }

    private static KnowledgeRoomEntity MapToEntity(KnowledgeRoom model)
    {
        return new KnowledgeRoomEntity
        {
            Id = model.Id,
            Name = model.Name,
            Description = model.Description ?? string.Empty,
            Color = model.Color,
            Icon = model.Icon,
            DocumentCount = model.DocumentCount,
            Tags = model.Tags,
            CreatedAt = model.CreatedAt,
            UpdatedAt = model.UpdatedAt
        };
    }
}
