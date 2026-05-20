using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Interfaces;

public interface IKnowledgeRoomService
{
    Task<IEnumerable<KnowledgeRoom>> ListRoomsAsync(string tenantId, string userId, CancellationToken ct = default);
    Task<KnowledgeRoom?> GetRoomAsync(string id, string tenantId, string userId, CancellationToken ct = default);
    Task<KnowledgeRoom> CreateRoomAsync(string tenantId, string userId, KnowledgeRoom room, CancellationToken ct = default);
    Task<KnowledgeRoom> UpdateRoomAsync(string tenantId, string userId, KnowledgeRoom room, CancellationToken ct = default);
    Task<bool> DeleteRoomAsync(string id, string tenantId, string userId, CancellationToken ct = default);

    Task<IEnumerable<KnowledgeRoomPermission>> GetRoomPermissionsAsync(string roomId, string tenantId, string userId, CancellationToken ct = default);
    Task<KnowledgeRoomPermission> AddOrUpdatePermissionAsync(string roomId, string targetUserId, KnowledgeRoomRole role, string tenantId, string currentUserId, CancellationToken ct = default);
    Task<bool> RemovePermissionAsync(string roomId, string targetUserId, string tenantId, string currentUserId, CancellationToken ct = default);
}
