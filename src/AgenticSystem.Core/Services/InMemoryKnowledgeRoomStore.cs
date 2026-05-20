using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Services;

public class InMemoryKnowledgeRoomStore : IKnowledgeRoomService
{
    private readonly List<KnowledgeRoom> _rooms = new();
    private readonly List<KnowledgeRoomPermission> _permissions = new();

    public Task<IEnumerable<KnowledgeRoom>> ListRoomsAsync(string tenantId, string userId, CancellationToken ct = default)
    {
        var accessibleRoomIds = _permissions.Where(p => p.UserId == userId).Select(p => p.RoomId).ToHashSet();
        var accessibleRooms = _rooms.Where(r => accessibleRoomIds.Contains(r.Id)).OrderByDescending(r => r.UpdatedAt);
        return Task.FromResult<IEnumerable<KnowledgeRoom>>(accessibleRooms);
    }

    public Task<KnowledgeRoom?> GetRoomAsync(string id, string tenantId, string userId, CancellationToken ct = default)
    {
        var hasAccess = _permissions.Any(p => p.RoomId == id && p.UserId == userId);
        if (!hasAccess) return Task.FromResult<KnowledgeRoom?>(null);

        return Task.FromResult(_rooms.FirstOrDefault(r => r.Id == id));
    }

    public Task<KnowledgeRoom> CreateRoomAsync(string tenantId, string userId, KnowledgeRoom room, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(room.Id))
        {
            var prop = room.GetType().GetProperty("Id");
            prop?.SetValue(room, Guid.NewGuid().ToString());
        }
        
        _rooms.Add(room);

        _permissions.Add(new KnowledgeRoomPermission
        {
            Id = Guid.NewGuid().ToString(),
            RoomId = room.Id,
            UserId = userId,
            Role = KnowledgeRoomRole.Admin,
            GrantedAt = DateTime.UtcNow
        });

        return Task.FromResult(room);
    }

    public Task<KnowledgeRoom> UpdateRoomAsync(string tenantId, string userId, KnowledgeRoom room, CancellationToken ct = default)
    {
        var permission = _permissions.FirstOrDefault(p => p.RoomId == room.Id && p.UserId == userId);
        if (permission == null || (permission.Role != KnowledgeRoomRole.Admin && permission.Role != KnowledgeRoomRole.Editor))
            throw new UnauthorizedAccessException("User does not have permission to update this room.");

        var existing = _rooms.FirstOrDefault(r => r.Id == room.Id);
        if (existing != null)
        {
            _rooms.Remove(existing);
        }
        _rooms.Add(room);
        return Task.FromResult(room);
    }

    public Task<bool> DeleteRoomAsync(string id, string tenantId, string userId, CancellationToken ct = default)
    {
        var permission = _permissions.FirstOrDefault(p => p.RoomId == id && p.UserId == userId);
        if (permission == null || permission.Role != KnowledgeRoomRole.Admin)
            return Task.FromResult(false);

        var existing = _rooms.FirstOrDefault(r => r.Id == id);
        if (existing == null) return Task.FromResult(false);
        _rooms.Remove(existing);
        _permissions.RemoveAll(p => p.RoomId == id);

        return Task.FromResult(true);
    }

    public Task<IEnumerable<KnowledgeRoomPermission>> GetRoomPermissionsAsync(string roomId, string tenantId, string userId, CancellationToken ct = default)
    {
        var permission = _permissions.FirstOrDefault(p => p.RoomId == roomId && p.UserId == userId);
        if (permission == null || permission.Role != KnowledgeRoomRole.Admin)
            throw new UnauthorizedAccessException("Only admins can view permissions.");

        return Task.FromResult<IEnumerable<KnowledgeRoomPermission>>(_permissions.Where(p => p.RoomId == roomId));
    }

    public Task<KnowledgeRoomPermission> AddOrUpdatePermissionAsync(string roomId, string targetUserId, KnowledgeRoomRole role, string tenantId, string currentUserId, CancellationToken ct = default)
    {
        var currentPerm = _permissions.FirstOrDefault(p => p.RoomId == roomId && p.UserId == currentUserId);
        if (currentPerm == null || currentPerm.Role != KnowledgeRoomRole.Admin)
            throw new UnauthorizedAccessException("Only admins can modify permissions.");

        var targetPerm = _permissions.FirstOrDefault(p => p.RoomId == roomId && p.UserId == targetUserId);
        if (targetPerm != null)
        {
            targetPerm.Role = role;
        }
        else
        {
            targetPerm = new KnowledgeRoomPermission
            {
                Id = Guid.NewGuid().ToString(),
                RoomId = roomId,
                UserId = targetUserId,
                Role = role,
                GrantedAt = DateTime.UtcNow
            };
            _permissions.Add(targetPerm);
        }

        return Task.FromResult(targetPerm);
    }

    public Task<bool> RemovePermissionAsync(string roomId, string targetUserId, string tenantId, string currentUserId, CancellationToken ct = default)
    {
        var currentPerm = _permissions.FirstOrDefault(p => p.RoomId == roomId && p.UserId == currentUserId);
        if (currentPerm == null || currentPerm.Role != KnowledgeRoomRole.Admin)
            throw new UnauthorizedAccessException("Only admins can modify permissions.");

        var targetPerm = _permissions.FirstOrDefault(p => p.RoomId == roomId && p.UserId == targetUserId);
        if (targetPerm == null) return Task.FromResult(false);

        _permissions.Remove(targetPerm);
        return Task.FromResult(true);
    }
}
