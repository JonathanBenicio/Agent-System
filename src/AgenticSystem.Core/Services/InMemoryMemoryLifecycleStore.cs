using System.Collections.Concurrent;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Services;

public class InMemoryMemoryLifecycleStore : IMemoryLifecycleStore
{
    private readonly ConcurrentDictionary<string, EnhancedMemoryEntry> _memories = new();

    public Task SaveAsync(EnhancedMemoryEntry entry, CancellationToken ct = default)
    {
        _memories[entry.Id] = entry;
        return Task.CompletedTask;
    }

    public Task<EnhancedMemoryEntry?> GetAsync(string id, CancellationToken ct = default)
    {
        _memories.TryGetValue(id, out var entry);
        return Task.FromResult(entry);
    }

    public Task<IReadOnlyList<EnhancedMemoryEntry>> ListAsync(string? sessionId = null, string? agentName = null, CancellationToken ct = default)
    {
        var query = _memories.Values.AsQueryable();
        if (!string.IsNullOrEmpty(sessionId)) query = query.Where(e => e.SessionId == sessionId);
        if (!string.IsNullOrEmpty(agentName)) query = query.Where(e => e.AgentName == agentName);
        return Task.FromResult<IReadOnlyList<EnhancedMemoryEntry>>(query.ToList());
    }

    public Task DeleteAsync(string id, CancellationToken ct = default)
    {
        _memories.TryRemove(id, out _);
        return Task.CompletedTask;
    }
}
