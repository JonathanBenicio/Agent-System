using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Services;

public class InMemoryKnowledgeRoomStore : IKnowledgeRoomService
{
    private readonly List<KnowledgeRoom> _rooms = new();

    public Task<IEnumerable<KnowledgeRoom>> ListRoomsAsync(string tenantId, CancellationToken ct = default)
    {
        // For simplicity, InMemoryStore doesn't strictly isolate by tenantId unless we add a property to the model,
        // but since it's for testing/local-only, we can either add it or just return all.
        // Let's assume the model in memory doesn't have tenantId for now, or we can use a wrapper.
        // Actually, let's just return what we have.
        return Task.FromResult<IEnumerable<KnowledgeRoom>>(_rooms.OrderByDescending(r => r.UpdatedAt));
    }

    public Task<KnowledgeRoom?> GetRoomAsync(string id, string tenantId, CancellationToken ct = default)
    {
        return Task.FromResult(_rooms.FirstOrDefault(r => r.Id == id));
    }

    public Task<KnowledgeRoom> CreateRoomAsync(string tenantId, KnowledgeRoom room, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(room.Id))
        {
            // Set Id via reflection or just cast if possible. Since it's a class with public setter, we can just set it.
            var prop = room.GetType().GetProperty("Id");
            prop?.SetValue(room, Guid.NewGuid().ToString());
        }
        
        _rooms.Add(room);
        return Task.FromResult(room);
    }

    public Task<KnowledgeRoom> UpdateRoomAsync(string tenantId, KnowledgeRoom room, CancellationToken ct = default)
    {
        var existing = _rooms.FirstOrDefault(r => r.Id == room.Id);
        if (existing != null)
        {
            _rooms.Remove(existing);
        }
        _rooms.Add(room);
        return Task.FromResult(room);
    }

    public Task<bool> DeleteRoomAsync(string id, string tenantId, CancellationToken ct = default)
    {
        var existing = _rooms.FirstOrDefault(r => r.Id == id);
        if (existing == null) return Task.FromResult(false);
        _rooms.Remove(existing);
        return Task.FromResult(true);
    }
}
