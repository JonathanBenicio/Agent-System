using System.Collections.Concurrent;
using System.Text;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Core.Services;

/// <summary>
/// Maturity Level 8 — Comprime histórico em princípios reutilizáveis e insights.
/// </summary>
public class SemanticCompressorService : ISemanticCompressor
{
    private readonly ISessionManager _sessionManager;
    private readonly ConcurrentBag<SemanticSummary> _summaries = new();
    private readonly ILogger<SemanticCompressorService> _logger;

    public SemanticCompressorService(ISessionManager sessionManager, ILogger<SemanticCompressorService> logger)
    {
        _sessionManager = sessionManager;
        _logger = logger;
    }

    public async Task<SemanticSummary> CompressSessionAsync(string sessionId)
    {
        var events = await _sessionManager.GetRecentEventsAsync(sessionId, 50);

        if (events.Count == 0)
        {
            return new SemanticSummary
            {
                SourceType = "session",
                SourceIds = new List<string> { sessionId },
                CompressedKnowledge = "Empty session — no events to compress.",
                OriginalTokenCount = 0,
                CompressedTokenCount = 0,
                CompressionRatio = 1.0
            };
        }

        var originalContent = new StringBuilder();
        var principles = new List<string>();
        var insights = new List<string>();
        var agentPatterns = new Dictionary<string, int>();

        foreach (var evt in events)
        {
            originalContent.AppendLine($"[{evt.AgentName}] Q: {evt.UserInput}");
            originalContent.AppendLine($"[{evt.AgentName}] A: {evt.AgentResponse}");

            // Track agent usage patterns
            if (!agentPatterns.ContainsKey(evt.AgentName))
                agentPatterns[evt.AgentName] = 0;
            agentPatterns[evt.AgentName]++;

            // Extract tool usage patterns
            foreach (var tool in evt.ToolsUsed)
            {
                insights.Add($"Tool '{tool}' used by {evt.AgentName}");
            }
        }

        // Generate principles from patterns
        var dominantAgent = agentPatterns.OrderByDescending(kv => kv.Value).FirstOrDefault();
        if (dominantAgent.Key != null)
        {
            principles.Add($"Primary agent for this session: {dominantAgent.Key} ({dominantAgent.Value} interactions)");
        }

        if (agentPatterns.Count > 1)
        {
            principles.Add($"Multi-agent session: {string.Join(", ", agentPatterns.Keys)}");
        }

        if (events.Count >= 10)
        {
            principles.Add("Extended interaction — consider breaking into smaller focused sessions");
        }

        // Deduplicate insights
        insights = insights.Distinct().ToList();

        // Build compressed knowledge
        var compressed = new StringBuilder();
        compressed.AppendLine($"Session: {sessionId}");
        compressed.AppendLine($"Events: {events.Count}");
        compressed.AppendLine($"Agents: {string.Join(", ", agentPatterns.Select(kv => $"{kv.Key}({kv.Value})"))}");

        if (principles.Count > 0)
        {
            compressed.AppendLine("Principles:");
            foreach (var p in principles) compressed.AppendLine($"  - {p}");
        }

        if (insights.Count > 0)
        {
            compressed.AppendLine("Insights:");
            foreach (var i in insights.Take(10)) compressed.AppendLine($"  - {i}");
        }

        var originalTokens = originalContent.Length / 4;
        var compressedTokens = compressed.Length / 4;

        var summary = new SemanticSummary
        {
            SourceType = "session",
            SourceIds = new List<string> { sessionId },
            CompressedKnowledge = compressed.ToString(),
            KeyPrinciples = principles,
            ActionableInsights = insights.Take(10).ToList(),
            OriginalTokenCount = originalTokens,
            CompressedTokenCount = compressedTokens,
            CompressionRatio = originalTokens > 0 ? (double)compressedTokens / originalTokens : 1.0
        };

        _summaries.Add(summary);
        _logger.LogInformation("Session {SessionId} compressed: {Original} → {Compressed} tokens (ratio: {Ratio:F2})",
            sessionId, originalTokens, compressedTokens, summary.CompressionRatio);

        return summary;
    }

    public Task<SemanticSummary> CompressChunksAsync(IEnumerable<string> chunkIds, string label)
    {
        var ids = chunkIds.ToList();

        var summary = new SemanticSummary
        {
            SourceType = "chunks",
            SourceIds = ids,
            CompressedKnowledge = $"Compressed {ids.Count} chunks under label: {label}",
            KeyPrinciples = new List<string> { $"Chunk group: {label}" },
            ActionableInsights = new List<string> { $"{ids.Count} chunks consolidated" },
            OriginalTokenCount = ids.Count * 200,
            CompressedTokenCount = 50,
            CompressionRatio = 50.0 / (ids.Count * 200)
        };

        _summaries.Add(summary);
        _logger.LogInformation("Compressed {Count} chunks under label '{Label}'", ids.Count, label);

        return Task.FromResult(summary);
    }

    public Task<IEnumerable<SemanticSummary>> GetInsightsAsync(string? sourceType = null, int count = 10)
    {
        var results = _summaries
            .Where(s => sourceType == null || s.SourceType == sourceType)
            .OrderByDescending(s => s.CreatedAt)
            .Take(count)
            .AsEnumerable();

        return Task.FromResult(results);
    }
}
