namespace AgenticSystem.Core.Interfaces;

/// <summary>
/// Store for managing agent-to-knowledge-room assignments.
/// Controls which agents can access which knowledge contexts.
/// </summary>
public interface IAgentKnowledgeRoomStore
{
    Task<IEnumerable<string>> GetRoomIdsForAgentAsync(string agentName, string tenantId, CancellationToken ct = default);
    Task<IEnumerable<string>> GetAgentNamesForRoomAsync(string roomId, string tenantId, CancellationToken ct = default);
    Task AssignRoomAsync(string agentName, string roomId, string tenantId, CancellationToken ct = default);
    Task UnassignRoomAsync(string agentName, string roomId, string tenantId, CancellationToken ct = default);
    Task SetRoomsForAgentAsync(string agentName, IEnumerable<string> roomIds, string tenantId, CancellationToken ct = default);
    Task<bool> IsRoomAssignedAsync(string agentName, string roomId, string tenantId, CancellationToken ct = default);
}
