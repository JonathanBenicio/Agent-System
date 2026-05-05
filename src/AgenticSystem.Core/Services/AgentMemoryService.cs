using System.Text.RegularExpressions;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Core.Services;

public class AgentMemoryService : IAgentMemoryService
{
    private static readonly Regex TokenRegex = new(@"\p{L}[\p{L}\p{Nd}_-]{2,}", RegexOptions.Compiled);

    private readonly IAgentMemoryStore _store;
    private readonly ILogger<AgentMemoryService> _logger;

    public AgentMemoryService(IAgentMemoryStore store, ILogger<AgentMemoryService> logger)
    {
        _store = store;
        _logger = logger;
    }

    public Task<AgentMemoryEntry> RememberAsync(AgentMemoryEntry entry, CancellationToken ct = default)
        => _store.SaveAsync(entry, ct);

    public async Task<IReadOnlyList<AgentMemoryEntry>> GetRelevantMemoriesAsync(
        string agentName,
        string userId,
        string prompt,
        int count = 5,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(agentName) || string.IsNullOrWhiteSpace(userId))
        {
            return [];
        }

        var entries = await _store.GetByAgentAsync(userId, agentName, ct);
        if (entries.Count == 0)
        {
            return entries;
        }

        var queryTerms = ExtractKeywords(prompt);
        var ranked = entries
            .Select(entry => new { Entry = entry, Score = ComputeScore(entry, queryTerms) })
            .OrderByDescending(item => item.Score)
            .ThenByDescending(item => item.Entry.Confidence)
            .ThenByDescending(item => item.Entry.LastUsedAt)
            .Take(count)
            .Select(item => item.Entry)
            .ToList();

        foreach (var entry in ranked)
        {
            entry.UsageCount++;
            entry.LastUsedAt = DateTime.UtcNow;
            await _store.SaveAsync(entry, ct);
        }

        return ranked;
    }

    public async Task RecordInteractionAsync(
        string sessionId,
        string agentName,
        UserContext context,
        string input,
        AgentResponse response,
        Reflection? reflection = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(context.UserId) || string.IsNullOrWhiteSpace(agentName))
        {
            return;
        }

        var keywords = ExtractKeywords($"{input} {response.Content} {string.Join(' ', context.Domains)}");
        var interactionMemory = new AgentMemoryEntry
        {
            UserId = context.UserId,
            AgentName = agentName,
            SessionId = sessionId,
            MemoryType = response.Success ? AgentMemoryType.Fact : AgentMemoryType.Reflection,
            Content = BuildInteractionMemoryContent(input, response),
            Confidence = response.Confidence?.Value ?? (response.Success ? 0.8 : 0.35),
            Source = "agent-execution",
            Keywords = keywords,
            Metadata = new Dictionary<string, string>
            {
                ["language"] = context.Language,
                ["role"] = context.Role,
                ["success"] = response.Success.ToString(),
                ["tier"] = response.AgentTier.ToString()
            }
        };

        await _store.SaveAsync(interactionMemory, ct);

        if (reflection is null)
        {
            return;
        }

        foreach (var lesson in reflection.LessonsLearned.Where(lesson => !string.IsNullOrWhiteSpace(lesson)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            await _store.SaveAsync(new AgentMemoryEntry
            {
                UserId = context.UserId,
                AgentName = agentName,
                SessionId = sessionId,
                MemoryType = AgentMemoryType.LearnedRule,
                Content = lesson,
                Confidence = Math.Max(interactionMemory.Confidence, 0.7),
                Source = "reflection",
                Keywords = ExtractKeywords(lesson),
                Metadata = new Dictionary<string, string>
                {
                    ["reflectionId"] = reflection.Id,
                    ["severity"] = reflection.Severity.ToString()
                }
            }, ct);
        }

        if (!string.IsNullOrWhiteSpace(reflection.ImprovementSuggestion))
        {
            await _store.SaveAsync(new AgentMemoryEntry
            {
                UserId = context.UserId,
                AgentName = agentName,
                SessionId = sessionId,
                MemoryType = AgentMemoryType.Correction,
                Content = reflection.ImprovementSuggestion,
                Confidence = 0.85,
                Source = "reflection-improvement",
                Keywords = ExtractKeywords(reflection.ImprovementSuggestion),
                Metadata = new Dictionary<string, string>
                {
                    ["reflectionId"] = reflection.Id,
                    ["severity"] = reflection.Severity.ToString()
                }
            }, ct);
        }

        _logger.LogDebug(
            "Agent memory recorded for {AgentName} / {UserId} with reflection {ReflectionId}",
            agentName,
            context.UserId,
            reflection.Id);
    }

    private static double ComputeScore(AgentMemoryEntry entry, IReadOnlyCollection<string> queryTerms)
    {
        var recencyWeight = Math.Max(0, 1 - (DateTime.UtcNow - entry.LastUsedAt).TotalDays / 30d);
        var usageWeight = Math.Min(entry.UsageCount, 10) * 0.05;
        var typeWeight = entry.MemoryType switch
        {
            AgentMemoryType.Correction => 1.2,
            AgentMemoryType.LearnedRule => 1.0,
            AgentMemoryType.Preference => 0.9,
            AgentMemoryType.Reflection => 0.7,
            _ => 0.6
        };

        if (queryTerms.Count == 0)
        {
            return entry.Confidence + recencyWeight + usageWeight + typeWeight;
        }

        var matchedKeywords = entry.Keywords.Count(keyword => queryTerms.Contains(keyword, StringComparer.OrdinalIgnoreCase));
        var matchedContentTerms = queryTerms.Count(term => entry.Content.Contains(term, StringComparison.OrdinalIgnoreCase));

        return entry.Confidence
            + recencyWeight
            + usageWeight
            + typeWeight
            + matchedKeywords * 0.8
            + matchedContentTerms * 0.4;
    }

    private static List<string> ExtractKeywords(string text)
        => TokenRegex.Matches(text ?? string.Empty)
            .Select(match => match.Value.ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(16)
            .ToList();

    private static string BuildInteractionMemoryContent(string input, AgentResponse response)
    {
        var inputSummary = Truncate(input, 240);
        var responseSummary = Truncate(response.Content, 320);
        return $"Pergunta recorrente: {inputSummary}\nResposta/análise anterior: {responseSummary}";
    }

    private static string Truncate(string text, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        return text.Length <= maxLength ? text : text[..maxLength] + "...";
    }
}