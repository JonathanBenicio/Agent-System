using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.AI;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Services;

/// <summary>
/// ML13 — Session Consolidator: resume sessões via LLM e extrai insights.
/// </summary>
public class SessionConsolidator : ISessionConsolidator
{
    private readonly IChatClient _chatClient;
    private readonly ILogger<SessionConsolidator> _logger;
    private readonly ConcurrentBag<SessionSummary> _summaries = [];

    public SessionConsolidator(IChatClient chatClient, ILogger<SessionConsolidator> logger)
    {
        _chatClient = chatClient;
        _logger = logger;
    }

    public async Task<SessionSummary> SummarizeSessionAsync(string sessionId, List<AgentEvent> events)
    {
        if (events.Count == 0)
        {
            return new SessionSummary
            {
                SessionId = sessionId,
                Summary = "Empty session — no events to summarize.",
                EventCount = 0
            };
        }

        var transcript = BuildTranscript(events);
        var prompt = $@"
Summarize this conversation session concisely.

SESSION TRANSCRIPT:
{transcript}

Return ONLY a valid JSON object:
{{
  ""summary"": ""2-3 sentence summary of what happened"",
  ""topicsDiscussed"": [""topic1"", ""topic2""],
  ""agentsUsed"": [""agent1"", ""agent2""]
}}";

        var request = new List<ChatMessage>
        {
            new(ChatRole.System, "You are a session summarizer. Return ONLY valid JSON."),
            new(ChatRole.User, prompt)
        };
        var options = new ChatOptions { Temperature = 0.1f };
        var response = await _chatClient.GetResponseAsync(request, options);
        var content = response.Text;

        if (string.IsNullOrWhiteSpace(content))
        {
            _logger.LogWarning("LLM summarization failed for session {SessionId}, using fallback", sessionId);
            return BuildFallbackSummary(sessionId, events);
        }

        try
        {
            var summary = ParseSummary(sessionId, content, events);
            _summaries.Add(summary);
            _logger.LogInformation("📋 Session {SessionId} summarized: {Topics}",
                sessionId, string.Join(", ", summary.TopicsDiscussed));
            return summary;
        }
        catch
        {
            var fallback = BuildFallbackSummary(sessionId, events);
            _summaries.Add(fallback);
            return fallback;
        }
    }

    public async Task<SessionInsights> ExtractInsightsAsync(string sessionId, List<AgentEvent> events)
    {
        if (events.Count == 0)
        {
            return new SessionInsights { SessionId = sessionId };
        }

        var transcript = BuildTranscript(events);
        var prompt = $@"
Extract key insights from this conversation session.

SESSION TRANSCRIPT:
{transcript}

Return ONLY a valid JSON object:
{{
  ""facts"": [""fact1"", ""fact2""],
  ""decisions"": [""decision1""],
  ""preferences"": [""preference1""],
  ""actionItems"": [""action1""]
}}

RULES:
- Facts: objective information stated or discovered
- Decisions: choices made during the session
- Preferences: user preferences revealed (style, format, behavior)
- ActionItems: things that need to be done after the session
";

        var insightRequest = new List<ChatMessage>
        {
            new(ChatRole.System, "You are a session insight extractor. Return ONLY valid JSON."),
            new(ChatRole.User, prompt)
        };
        var insightOptions = new ChatOptions { Temperature = 0.1f };
        var response = await _chatClient.GetResponseAsync(insightRequest, insightOptions);
        var content = response.Text;

        if (string.IsNullOrWhiteSpace(content))
        {
            return BuildFallbackInsights(sessionId, events);
        }

        try
        {
            return ParseInsights(sessionId, content);
        }
        catch
        {
            return BuildFallbackInsights(sessionId, events);
        }
    }

    public Task<IEnumerable<SessionSummary>> GetRelevantSummariesAsync(string query, int maxResults = 5)
    {
        // Simple keyword-based relevance for now
        var queryTerms = query.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var relevant = _summaries
            .OrderByDescending(s =>
            {
                var text = $"{s.Summary} {string.Join(" ", s.TopicsDiscussed)}".ToLowerInvariant();
                return queryTerms.Count(t => text.Contains(t));
            })
            .Take(maxResults)
            .ToList();

        return Task.FromResult<IEnumerable<SessionSummary>>(relevant);
    }

    private static string BuildTranscript(List<AgentEvent> events)
    {
        var sb = new StringBuilder();
        foreach (var e in events.OrderBy(e => e.Timestamp))
        {
            sb.AppendLine($"[{e.Timestamp:HH:mm}] User: {Truncate(e.UserInput, 200)}");
            sb.AppendLine($"[{e.Timestamp:HH:mm}] {e.AgentName}: {Truncate(e.AgentResponse, 200)}");
            sb.AppendLine();
        }
        return sb.ToString();
    }

    private static SessionSummary BuildFallbackSummary(string sessionId, List<AgentEvent> events)
    {
        var agents = events.Select(e => e.AgentName).Distinct().ToList();
        return new SessionSummary
        {
            SessionId = sessionId,
            Summary = $"Session with {events.Count} interactions across {agents.Count} agent(s).",
            TopicsDiscussed = events.Select(e => e.AgentName).Distinct().ToList(),
            AgentsUsed = agents,
            EventCount = events.Count
        };
    }

    private static SessionInsights BuildFallbackInsights(string sessionId, List<AgentEvent> events)
    {
        return new SessionInsights
        {
            SessionId = sessionId,
            Facts = [$"Session had {events.Count} interactions"],
            Decisions = [],
            Preferences = [],
            ActionItems = []
        };
    }

    private static SessionSummary ParseSummary(string sessionId, string content, List<AgentEvent> events)
    {
        var jsonStart = content.IndexOf('{');
        var jsonEnd = content.LastIndexOf('}');
        if (jsonStart < 0 || jsonEnd <= jsonStart)
            throw new InvalidOperationException("No JSON found");

        var json = content[jsonStart..(jsonEnd + 1)];
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        return new SessionSummary
        {
            SessionId = sessionId,
            Summary = root.TryGetProperty("summary", out var s) ? s.GetString() ?? "" : "",
            TopicsDiscussed = ParseStringArray(root, "topicsDiscussed"),
            AgentsUsed = ParseStringArray(root, "agentsUsed"),
            EventCount = events.Count
        };
    }

    private static SessionInsights ParseInsights(string sessionId, string content)
    {
        var jsonStart = content.IndexOf('{');
        var jsonEnd = content.LastIndexOf('}');
        if (jsonStart < 0 || jsonEnd <= jsonStart)
            throw new InvalidOperationException("No JSON found");

        var json = content[jsonStart..(jsonEnd + 1)];
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        return new SessionInsights
        {
            SessionId = sessionId,
            Facts = ParseStringArray(root, "facts"),
            Decisions = ParseStringArray(root, "decisions"),
            Preferences = ParseStringArray(root, "preferences"),
            ActionItems = ParseStringArray(root, "actionItems")
        };
    }

    private static List<string> ParseStringArray(JsonElement root, string property)
    {
        if (!root.TryGetProperty(property, out var arr) || arr.ValueKind != JsonValueKind.Array)
            return [];

        return arr.EnumerateArray()
            .Select(e => e.GetString())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Cast<string>()
            .ToList();
    }

    private static string Truncate(string text, int max)
        => text.Length <= max ? text : text[..max] + "...";
}
