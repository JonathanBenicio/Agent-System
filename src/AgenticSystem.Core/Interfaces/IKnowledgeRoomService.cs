using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Interfaces;

public interface IKnowledgeRoomService
{
    Task<IEnumerable<KnowledgeRoom>> ListRoomsAsync(string tenantId, CancellationToken ct = default);
    Task<KnowledgeRoom?> GetRoomAsync(string id, string tenantId, CancellationToken ct = default);
    Task<KnowledgeRoom> CreateRoomAsync(string tenantId, KnowledgeRoom room, CancellationToken ct = default);
    Task<KnowledgeRoom> UpdateRoomAsync(string tenantId, KnowledgeRoom room, CancellationToken ct = default);
    Task<bool> DeleteRoomAsync(string id, string tenantId, CancellationToken ct = default);
}
