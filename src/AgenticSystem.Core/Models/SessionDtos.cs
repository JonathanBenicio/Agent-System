using System.Text.Json;
using AgenticSystem.Core.Interfaces;

namespace AgenticSystem.Core.Models;

public record SessionListItemDto(
    string Id,
    string Title,
    DateTime LastActivity,
    int MessageCount,
    string? Summary);

public record SessionDetailDto(
    string Id,
    string Title,
    DateTime StartedAt,
    DateTime? EndedAt,
    List<ChatMessageDto> Messages,
    SessionSummaryDto? Summary,
    SessionInsightsDto? Insights);

public record ChatMessageDto(
    string Id,
    string Role,
    string Content,
    string? AgentName,
    int? AgentTier,
    List<string>? Actions,
    List<string>? Tools,
    bool? Success,
    DateTime Timestamp,
    bool? MemoryInjected = null);

public record SessionSummaryDto(
    string Summary,
    List<string> TopicsDiscussed,
    List<string> AgentsUsed,
    int EventCount);

public record SessionInsightsDto(
    List<string> Facts,
    List<string> Decisions,
    List<string> Preferences,
    List<string> ActionItems);

public record UpdateSessionTitleRequest(string Title);

public static class SessionDtoMapper
{
    public static SessionListItemDto ToListItem(SessionData session)
    {
        var title = session.RuntimeSettings.TryGetValue("title", out var t) && !string.IsNullOrEmpty(t)
            ? t
            : $"Session {session.Id[..8]}";

        var summary = session.Summary?.Summary;

        return new SessionListItemDto(
            session.Id,
            title,
            session.EndedAt ?? session.StartedAt,
            session.Events.Count,
            summary);
    }

    public static SessionDetailDto ToDetail(SessionData session)
    {
        var title = session.RuntimeSettings.TryGetValue("title", out var t) && !string.IsNullOrEmpty(t)
            ? t
            : $"Session {session.Id[..8]}";

        var messages = session.Events
            .OrderBy(e => e.Timestamp)
            .SelectMany(e =>
            {
                var memoryInjected = e.Context.TryGetValue("memory_injected", out var mi) && mi is bool b && b;
                
                return new[]
                {
                    new ChatMessageDto(
                        $"user_{e.Id}",
                        "user",
                        e.UserInput,
                        null,
                        null,
                        null,
                        null,
                        null,
                        e.Timestamp,
                        memoryInjected),
                    new ChatMessageDto(
                        $"assistant_{e.Id}",
                        "assistant",
                        e.AgentResponse,
                        e.AgentName,
                        (int)e.AgentTier,
                        e.ActionsPerformed,
                        e.ToolsUsed,
                        null,
                        e.Timestamp),
                };
            })
            .ToList();

        var summaryDto = session.Summary != null
            ? new SessionSummaryDto(
                session.Summary.Summary,
                session.Summary.TopicsDiscussed,
                session.Summary.AgentsUsed,
                session.Summary.EventCount)
            : null;

        var insightsDto = session.Insights != null
            ? new SessionInsightsDto(
                session.Insights.Facts,
                session.Insights.Decisions,
                session.Insights.Preferences,
                session.Insights.ActionItems)
            : null;

        return new SessionDetailDto(
            session.Id,
            title,
            session.StartedAt,
            session.EndedAt,
            messages,
            summaryDto,
            insightsDto);
    }

    public static SessionSummaryDto ToSummary(SessionSummary summary)
    {
        return new SessionSummaryDto(
            summary.Summary,
            summary.TopicsDiscussed,
            summary.AgentsUsed,
            summary.EventCount);
    }

    public static SessionInsightsDto ToInsights(SessionInsights insights)
    {
        return new SessionInsightsDto(
            insights.Facts,
            insights.Decisions,
            insights.Preferences,
            insights.ActionItems);
    }

    public static List<ChatMessageDto> ToMessages(SessionData session)
    {
        return session.Events
            .OrderBy(e => e.Timestamp)
            .SelectMany(e =>
            {
                var memoryInjected = e.Context.TryGetValue("memory_injected", out var mi) && mi is bool b && b;

                return new[]
                {
                    new ChatMessageDto(
                        $"user_{e.Id}",
                        "user",
                        e.UserInput,
                        null,
                        null,
                        null,
                        null,
                        null,
                        e.Timestamp,
                        memoryInjected),
                    new ChatMessageDto(
                        $"assistant_{e.Id}",
                        "assistant",
                        e.AgentResponse,
                        e.AgentName,
                        (int)e.AgentTier,
                        e.ActionsPerformed,
                        e.ToolsUsed,
                        null,
                        e.Timestamp),
                };
            })
            .ToList();
    }
}
