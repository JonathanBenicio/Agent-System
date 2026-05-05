using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Interfaces;

public interface IAgentMemoryStore
{
    Task<AgentMemoryEntry> SaveAsync(AgentMemoryEntry entry, CancellationToken ct = default);
    Task<IReadOnlyList<AgentMemoryEntry>> GetByAgentAsync(string userId, string agentName, CancellationToken ct = default);
}

public interface IAgentMemoryService
{
    Task<AgentMemoryEntry> RememberAsync(AgentMemoryEntry entry, CancellationToken ct = default);
    Task<IReadOnlyList<AgentMemoryEntry>> GetRelevantMemoriesAsync(
        string agentName,
        string userId,
        string prompt,
        int count = 5,
        CancellationToken ct = default);
    Task RecordInteractionAsync(
        string sessionId,
        string agentName,
        UserContext context,
        string input,
        AgentResponse response,
        Reflection? reflection = null,
        CancellationToken ct = default);
}