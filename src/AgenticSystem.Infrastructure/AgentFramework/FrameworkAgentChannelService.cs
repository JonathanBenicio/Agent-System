using System.Text;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Infrastructure.AgentFramework;

public class FrameworkAgentChannelService : IAgentChannelService
{
    private const string ChannelEventSource = "AgentFramework.Channel";
    private const string ChannelMessageKey = "agentChannelMessage";
    private const string ChannelSourceKey = "agentChannelSource";
    private const string ChannelTargetKey = "agentChannelTarget";
    private const string ChannelKindKey = "agentChannelKind";

    private readonly ISessionManager _sessionManager;
    private readonly ILogger<FrameworkAgentChannelService> _logger;

    public FrameworkAgentChannelService(ISessionManager sessionManager, ILogger<FrameworkAgentChannelService> logger)
    {
        _sessionManager = sessionManager;
        _logger = logger;
    }

    public async Task<AgentChannelMessage> PublishAsync(
        string sessionId,
        string sourceAgent,
        string targetAgent,
        string content,
        AgentChannelKind kind,
        Dictionary<string, object>? metadata = null,
        CancellationToken ct = default)
    {
        var message = new AgentChannelMessage
        {
            SessionId = sessionId,
            SourceAgent = sourceAgent,
            TargetAgent = targetAgent,
            Content = content,
            Kind = kind,
            Metadata = metadata ?? new Dictionary<string, object>()
        };

        await _sessionManager.AddEventAsync(sessionId, new AgentEvent
        {
            SessionId = sessionId,
            AgentName = ChannelEventSource,
            UserInput = "[agent-channel]",
            AgentResponse = content,
            Context = new Dictionary<string, object>(message.Metadata)
            {
                [ChannelMessageKey] = message.Id,
                [ChannelSourceKey] = sourceAgent,
                [ChannelTargetKey] = targetAgent,
                [ChannelKindKey] = kind.ToString()
            }
        });

        _logger.LogDebug(
            "Agent channel published in session {SessionId}: {SourceAgent} -> {TargetAgent} ({Kind})",
            sessionId,
            sourceAgent,
            targetAgent,
            kind);

        return message;
    }

    public async Task<IReadOnlyList<AgentChannelMessage>> GetMessagesAsync(
        string sessionId,
        string targetAgent,
        int maxCount = 10,
        CancellationToken ct = default)
    {
        var recentEvents = await _sessionManager.GetRecentEventsAsync(sessionId, 200);

        var messages = recentEvents
            .Where(agentEvent => agentEvent.AgentName == ChannelEventSource && agentEvent.Context is not null)
            .Select(agentEvent => MapToMessage(agentEvent, sessionId))
            .Where(message => message is not null)
            .Select(message => message!)
            .Where(message => message.TargetAgent.Equals(targetAgent, StringComparison.OrdinalIgnoreCase)
                || message.TargetAgent.Equals("*", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(message => message.CreatedAt)
            .Take(maxCount)
            .OrderBy(message => message.CreatedAt)
            .ToList();

        return messages;
    }

    public async Task<string> BuildChannelContextAsync(
        string sessionId,
        string targetAgent,
        string input,
        int maxCount = 5,
        CancellationToken ct = default)
    {
        var messages = await GetMessagesAsync(sessionId, targetAgent, maxCount, ct);
        if (messages.Count == 0)
        {
            return input;
        }

        var builder = new StringBuilder();
        builder.AppendLine("[Native Agent Channel Context]");
        foreach (var message in messages)
        {
            builder.Append("- From ");
            builder.Append(message.SourceAgent);
            builder.Append(" via ");
            builder.Append(message.Kind);
            builder.Append(": ");
            builder.AppendLine(message.Content);
        }

        builder.AppendLine();
        builder.AppendLine("[Assigned Input]");
        builder.Append(input);
        return builder.ToString();
    }

    private static AgentChannelMessage? MapToMessage(AgentEvent agentEvent, string sessionId)
    {
        if (agentEvent.Context is null)
        {
            return null;
        }

        if (!agentEvent.Context.TryGetValue(ChannelSourceKey, out var rawSource)
            || !agentEvent.Context.TryGetValue(ChannelTargetKey, out var rawTarget))
        {
            return null;
        }

        var kind = AgentChannelKind.Direct;
        if (agentEvent.Context.TryGetValue(ChannelKindKey, out var rawKind)
            && rawKind is not null
            && Enum.TryParse<AgentChannelKind>(rawKind.ToString(), out var parsedKind))
        {
            kind = parsedKind;
        }

        var metadata = new Dictionary<string, object>(agentEvent.Context);
        return new AgentChannelMessage
        {
            Id = agentEvent.Context.TryGetValue(ChannelMessageKey, out var rawId) && rawId is not null
                ? rawId.ToString() ?? Guid.NewGuid().ToString("N")
                : Guid.NewGuid().ToString("N"),
            SessionId = sessionId,
            SourceAgent = rawSource?.ToString() ?? "unknown",
            TargetAgent = rawTarget?.ToString() ?? "*",
            Kind = kind,
            Content = agentEvent.AgentResponse,
            CreatedAt = agentEvent.Timestamp,
            Metadata = metadata
        };
    }
}