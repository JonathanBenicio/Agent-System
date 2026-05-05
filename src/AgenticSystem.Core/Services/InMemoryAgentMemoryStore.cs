using System.Collections.Concurrent;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Services;

public class InMemoryAgentMemoryStore : IAgentMemoryStore
{
    private readonly ConcurrentDictionary<string, AgentMemoryEntry> _entries = new(StringComparer.OrdinalIgnoreCase);

    public Task<AgentMemoryEntry> SaveAsync(AgentMemoryEntry entry, CancellationToken ct = default)
    {
        _entries[entry.Id] = entry;
        return Task.FromResult(entry);
    }

    public Task<IReadOnlyList<AgentMemoryEntry>> GetByAgentAsync(string userId, string agentName, CancellationToken ct = default)
    {
        var entries = _entries.Values
            .Where(entry => entry.IsActive)
            .Where(entry => entry.UserId.Equals(userId, StringComparison.OrdinalIgnoreCase))
            .Where(entry => entry.AgentName.Equals(agentName, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(entry => entry.LastUsedAt)
            .ThenByDescending(entry => entry.CreatedAt)
            .ToList();

        return Task.FromResult<IReadOnlyList<AgentMemoryEntry>>(entries);
    }
}