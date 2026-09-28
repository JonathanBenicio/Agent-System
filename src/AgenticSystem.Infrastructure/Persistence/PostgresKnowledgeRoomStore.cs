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

    public async Task<IEnumerable<KnowledgeRoom>> ListRoomsAsync(string tenantId, string userId, CancellationToken ct = default)
    {
        var accessibleRoomIds = await _dbContext.Set<KnowledgeRoomPermissionEntity>()
            .Where(p => p.TenantId == tenantId && p.UserId == userId)
            .Select(p => p.RoomId)
            .ToListAsync(ct);

        var entities = await _dbContext.Set<KnowledgeRoomEntity>()
            .Where(r => r.TenantId == tenantId && accessibleRoomIds.Contains(r.Id))
            .OrderByDescending(r => r.UpdatedAt)
            .ToListAsync(ct);

        return entities.Select(MapToModel);
    }

    public async Task<KnowledgeRoom?> GetRoomAsync(string id, string tenantId, string userId, CancellationToken ct = default)
    {
        var hasAccess = await _dbContext.Set<KnowledgeRoomPermissionEntity>()
            .AnyAsync(p => p.TenantId == tenantId && p.RoomId == id && p.UserId == userId, ct);

        if (!hasAccess) return null;

        var entity = await _dbContext.Set<KnowledgeRoomEntity>()
            .FirstOrDefaultAsync(r => r.Id == id && r.TenantId == tenantId, ct);

        return entity != null ? MapToModel(entity) : null;
    }

    public async Task<KnowledgeRoom> CreateRoomAsync(string tenantId, string userId, KnowledgeRoom room, CancellationToken ct = default)
    {
        var entity = MapToEntity(room);
        entity.TenantId = tenantId;
        
        if (string.IsNullOrEmpty(entity.Id))
        {
            entity.Id = Guid.NewGuid().ToString();
            room.Id = entity.Id;
        }
        
        entity.CreatedAt = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;
        
        _dbContext.Set<KnowledgeRoomEntity>().Add(entity);

        var permission = new KnowledgeRoomPermissionEntity
        {
            Id = Guid.NewGuid().ToString(),
            TenantId = tenantId,
            RoomId = entity.Id,
            UserId = userId,
            Role = KnowledgeRoomRole.Admin.ToString(),
            GrantedAt = DateTime.UtcNow
        };
        _dbContext.Set<KnowledgeRoomPermissionEntity>().Add(permission);

        await _dbContext.SaveChangesAsync(ct);
        
        return MapToModel(entity);
    }

    public async Task<KnowledgeRoom> UpdateRoomAsync(string tenantId, string userId, KnowledgeRoom room, CancellationToken ct = default)
    {
        var permission = await _dbContext.Set<KnowledgeRoomPermissionEntity>()
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.RoomId == room.Id && p.UserId == userId, ct);

        if (permission == null || (permission.Role != KnowledgeRoomRole.Admin.ToString() && permission.Role != KnowledgeRoomRole.Editor.ToString()))
        {
            throw new UnauthorizedAccessException("User does not have permission to update this room.");
        }

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

    public async Task<bool> DeleteRoomAsync(string id, string tenantId, string userId, CancellationToken ct = default)
    {
        var permission = await _dbContext.Set<KnowledgeRoomPermissionEntity>()
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.RoomId == id && p.UserId == userId, ct);

        if (permission == null || permission.Role != KnowledgeRoomRole.Admin.ToString())
        {
            return false;
        }

        var entity = await _dbContext.Set<KnowledgeRoomEntity>()
            .FirstOrDefaultAsync(r => r.Id == id && r.TenantId == tenantId, ct);
            
        if (entity == null) return false;

        var permissions = await _dbContext.Set<KnowledgeRoomPermissionEntity>()
            .Where(p => p.TenantId == tenantId && p.RoomId == id).ToListAsync(ct);
        
        _dbContext.Set<KnowledgeRoomPermissionEntity>().RemoveRange(permissions);
        _dbContext.Set<KnowledgeRoomEntity>().Remove(entity);
        await _dbContext.SaveChangesAsync(ct);
        
        return true;
    }

    public async Task<IEnumerable<KnowledgeRoomPermission>> GetRoomPermissionsAsync(string roomId, string tenantId, string userId, CancellationToken ct = default)
    {
        var permission = await _dbContext.Set<KnowledgeRoomPermissionEntity>()
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.RoomId == roomId && p.UserId == userId, ct);

        if (permission == null || permission.Role != KnowledgeRoomRole.Admin.ToString())
        {
            throw new UnauthorizedAccessException("Only admins can view permissions.");
        }

        var permissions = await _dbContext.Set<KnowledgeRoomPermissionEntity>()
            .Where(p => p.TenantId == tenantId && p.RoomId == roomId)
            .ToListAsync(ct);

        return permissions.Select(MapToPermissionModel);
    }

    public async Task<KnowledgeRoomPermission> AddOrUpdatePermissionAsync(string roomId, string targetUserId, KnowledgeRoomRole role, string tenantId, string currentUserId, CancellationToken ct = default)
    {
        var currentPerm = await _dbContext.Set<KnowledgeRoomPermissionEntity>()
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.RoomId == roomId && p.UserId == currentUserId, ct);

        if (currentPerm == null || currentPerm.Role != KnowledgeRoomRole.Admin.ToString())
        {
            throw new UnauthorizedAccessException("Only admins can modify permissions.");
        }

        var targetPerm = await _dbContext.Set<KnowledgeRoomPermissionEntity>()
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.RoomId == roomId && p.UserId == targetUserId, ct);

        if (targetPerm != null)
        {
            targetPerm.Role = role.ToString();
        }
        else
        {
            targetPerm = new KnowledgeRoomPermissionEntity
            {
                Id = Guid.NewGuid().ToString(),
                TenantId = tenantId,
                RoomId = roomId,
                UserId = targetUserId,
                Role = role.ToString(),
                GrantedAt = DateTime.UtcNow
            };
            _dbContext.Set<KnowledgeRoomPermissionEntity>().Add(targetPerm);
        }

        await _dbContext.SaveChangesAsync(ct);
        return MapToPermissionModel(targetPerm);
    }

    public async Task<bool> RemovePermissionAsync(string roomId, string targetUserId, string tenantId, string currentUserId, CancellationToken ct = default)
    {
        var currentPerm = await _dbContext.Set<KnowledgeRoomPermissionEntity>()
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.RoomId == roomId && p.UserId == currentUserId, ct);

        if (currentPerm == null || currentPerm.Role != KnowledgeRoomRole.Admin.ToString())
        {
            throw new UnauthorizedAccessException("Only admins can modify permissions.");
        }

        var targetPerm = await _dbContext.Set<KnowledgeRoomPermissionEntity>()
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.RoomId == roomId && p.UserId == targetUserId, ct);

        if (targetPerm == null) return false;

        _dbContext.Set<KnowledgeRoomPermissionEntity>().Remove(targetPerm);
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

    private static KnowledgeRoomPermission MapToPermissionModel(KnowledgeRoomPermissionEntity entity)
    {
        return new KnowledgeRoomPermission
        {
            Id = entity.Id,
            RoomId = entity.RoomId,
            UserId = entity.UserId,
            Role = Enum.TryParse<KnowledgeRoomRole>(entity.Role, out var role) ? role : KnowledgeRoomRole.Reader,
            GrantedAt = entity.GrantedAt
        };
    }
}
