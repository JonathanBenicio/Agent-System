using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Core.Services;

/// <summary>
/// Maturity Level 8 — Comprime histórico em princípios reutilizáveis e insights.
/// </summary>
public class SemanticCompressorService : ISemanticCompressor
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ConcurrentBag<SemanticSummary> _summaries = new();
    private readonly ILogger<SemanticCompressorService> _logger;

    private static readonly HashSet<string> Stopwords = new(StringComparer.OrdinalIgnoreCase)
    {
        "para", "com", "sem", "sobre", "entre", "uma", "umas", "uns", "como",
        "that", "this", "with", "from", "into", "about", "have", "will", "were",
        "quando", "onde", "qual", "quais", "because", "there", "their", "them",
        "você", "voce", "need", "want", "your", "user", "agent", "session"
    };

    public SemanticCompressorService(IServiceProvider serviceProvider, ILogger<SemanticCompressorService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task<SemanticSummary> CompressSessionAsync(string sessionId)
    {
        var sessionManager = _serviceProvider.GetRequiredService<ISessionManager>();
        var events = await sessionManager.GetRecentEventsAsync(sessionId, 50);

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

    public Task<SemanticSummary> CompressRankedChunksAsync(IEnumerable<RankedChunk> chunks, string label)
    {
        var rankedChunks = chunks
            .Where(chunk => !string.IsNullOrWhiteSpace(chunk.Content))
            .OrderBy(chunk => chunk.Rank == 0 ? int.MaxValue : chunk.Rank)
            .ThenByDescending(chunk => chunk.ReRankedScore)
            .ToList();

        if (rankedChunks.Count == 0)
        {
            return Task.FromResult(new SemanticSummary
            {
                SourceType = "ranked-chunks",
                CompressedKnowledge = "Nenhum chunk elegível para compressão.",
                OriginalTokenCount = 0,
                CompressedTokenCount = 0,
                CompressionRatio = 1.0
            });
        }

        var sourceIds = rankedChunks.Select(chunk => chunk.Id).ToList();
        var sources = rankedChunks
            .Select(chunk => chunk.Source)
            .Where(source => !string.IsNullOrWhiteSpace(source))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(4)
            .ToList();
        var sections = rankedChunks
            .Select(chunk => chunk.Section)
            .Where(section => !string.IsNullOrWhiteSpace(section))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(4)
            .ToList();
        var recurringTerms = ExtractFrequentTerms(rankedChunks.Select(chunk => chunk.Content), maxTerms: 8);
        var representativeSentences = rankedChunks
            .Select(chunk => ExtractRepresentativeSentence(chunk.Content))
            .Where(sentence => !string.IsNullOrWhiteSpace(sentence))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToList();

        var principles = new List<string>();
        if (sources.Count > 0)
        {
            principles.Add($"Fontes dominantes: {string.Join(", ", sources)}");
        }

        if (sections.Count > 0)
        {
            principles.Add($"Seções relevantes: {string.Join(", ", sections)}");
        }

        if (recurringTerms.Count > 0)
        {
            principles.Add($"Termos recorrentes: {string.Join(", ", recurringTerms)}");
        }

        var insights = new List<string>();
        insights.AddRange(representativeSentences);

        foreach (var chunk in rankedChunks.Where(chunk => chunk.Metadata.ContainsKey("stalePenalized")).Take(2))
        {
            insights.Add($"Chunk {chunk.Id} recebeu penalização por frescor.");
        }

        var compressed = new StringBuilder();
        compressed.AppendLine($"Tema: {label}");

        if (principles.Count > 0)
        {
            compressed.AppendLine("Sinais agregados:");
            foreach (var principle in principles)
            {
                compressed.AppendLine($"- {principle}");
            }
        }

        if (representativeSentences.Count > 0)
        {
            compressed.AppendLine("Síntese:");
            foreach (var sentence in representativeSentences)
            {
                compressed.AppendLine($"- {sentence}");
            }
        }

        var originalTokens = EstimateTokens(string.Join('\n', rankedChunks.Select(chunk => chunk.Content)));
        var compressedTokens = EstimateTokens(compressed.ToString());

        var summary = new SemanticSummary
        {
            SourceType = "ranked-chunks",
            SourceIds = sourceIds,
            CompressedKnowledge = compressed.ToString().TrimEnd(),
            KeyPrinciples = principles,
            ActionableInsights = insights,
            OriginalTokenCount = originalTokens,
            CompressedTokenCount = compressedTokens,
            CompressionRatio = originalTokens > 0 ? (double)compressedTokens / originalTokens : 1.0
        };

        _summaries.Add(summary);
        _logger.LogInformation(
            "Ranked chunks compressed: {Count} chunks, {Original} → {Compressed} tokens (ratio: {Ratio:F2})",
            rankedChunks.Count,
            originalTokens,
            compressedTokens,
            summary.CompressionRatio);

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

    private static List<string> ExtractFrequentTerms(IEnumerable<string> texts, int maxTerms)
    {
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var text in texts)
        {
            foreach (var term in Regex.Split(text, @"[^\p{L}\p{Nd}]+"))
            {
                if (string.IsNullOrWhiteSpace(term) || term.Length < 4 || Stopwords.Contains(term))
                {
                    continue;
                }

                counts[term] = counts.TryGetValue(term, out var count) ? count + 1 : 1;
            }
        }

        return counts
            .OrderByDescending(entry => entry.Value)
            .ThenBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
            .Take(maxTerms)
            .Select(entry => entry.Key)
            .ToList();
    }

    private static string ExtractRepresentativeSentence(string content)
    {
        var sentence = Regex.Split(content.Trim(), @"(?<=[\.!?])\s+")
            .FirstOrDefault(part => !string.IsNullOrWhiteSpace(part))
            ?? content.Trim();

        return sentence.Length > 180 ? sentence[..177] + "..." : sentence;
    }

    private static int EstimateTokens(string text)
        => string.IsNullOrWhiteSpace(text) ? 0 : (int)Math.Ceiling(text.Length / 4.0);
}
