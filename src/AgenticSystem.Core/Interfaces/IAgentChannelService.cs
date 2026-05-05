using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Interfaces;

public interface IAgentChannelService
{
    Task<AgentChannelMessage> PublishAsync(
        string sessionId,
        string sourceAgent,
        string targetAgent,
        string content,
        AgentChannelKind kind,
        Dictionary<string, object>? metadata = null,
        CancellationToken ct = default);

    Task<IReadOnlyList<AgentChannelMessage>> GetMessagesAsync(
        string sessionId,
        string targetAgent,
        int maxCount = 10,
        CancellationToken ct = default);

    Task<string> BuildChannelContextAsync(
        string sessionId,
        string targetAgent,
        string input,
        int maxCount = 5,
        CancellationToken ct = default);
}