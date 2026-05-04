using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Infrastructure.RAG;

/// <summary>
/// Re-ranker heurístico — reordena candidatos usando TF-IDF simplificado,
/// boost por metadata e penalização de overlap.
/// Não requer modelo externo (cross-encoder).
/// </summary>
public class HeuristicReRanker : IReRanker
{
    private readonly ILogger<HeuristicReRanker> _logger;

    public HeuristicReRanker(ILogger<HeuristicReRanker> logger)
    {
        _logger = logger;
    }

    public Task<IReadOnlyList<RankedChunk>> ReRankAsync(
        string query, IReadOnlyList<SearchMatch> candidates, int topK = 5, CancellationToken ct = default)
    {
        var queryTerms = Tokenize(query);
        var scored = new List<RankedChunk>();

        foreach (var candidate in candidates)
        {
            var contentTerms = Tokenize(candidate.Content);

            // Term frequency score
            var tfScore = CalculateTFScore(queryTerms, contentTerms);

            // Exact phrase bonus
            var phraseBonus = candidate.Content.Contains(query, StringComparison.OrdinalIgnoreCase) ? 0.2 : 0.0;

            // Metadata bonus (has section title, source, type match)
            var metaBonus = CalculateMetadataBonus(candidate.Metadata, queryTerms);

            // Original vector score weight
            var vectorWeight = candidate.Score * 0.4;

            // Overlap penalty (if chunk indicates overlap, slight penalty to prefer original chunks)
            var overlapPenalty = 0.0;
            if (candidate.Metadata.TryGetValue("has_overlap", out var ov) && ov == "True")
                overlapPenalty = -0.05;

            var finalScore = Math.Min(vectorWeight + tfScore * 0.3 + phraseBonus + metaBonus + overlapPenalty, 1.0);

            scored.Add(new RankedChunk
            {
                Id = candidate.Id,
                Content = candidate.Content,
                OriginalScore = candidate.Score,
                ReRankedScore = Math.Max(finalScore, 0),
                Source = candidate.Metadata.GetValueOrDefault("source", ""),
                Section = candidate.Metadata.GetValueOrDefault("section", ""),
                Metadata = candidate.Metadata
            });
        }

        var ranked = scored
            .OrderByDescending(r => r.ReRankedScore)
            .Take(topK)
            .Select((r, i) => { r.Rank = i + 1; return r; })
            .ToList();

        _logger.LogDebug("🔄 Re-ranked {In} → {Out} candidates (query: {Query})",
            candidates.Count, ranked.Count, query.Length > 50 ? query[..50] + "..." : query);

        return Task.FromResult<IReadOnlyList<RankedChunk>>(ranked);
    }

    private static double CalculateTFScore(HashSet<string> queryTerms, HashSet<string> contentTerms)
    {
        if (queryTerms.Count == 0) return 0;
        var matches = queryTerms.Count(q => contentTerms.Contains(q));
        return (double)matches / queryTerms.Count;
    }

    private static double CalculateMetadataBonus(Dictionary<string, string> metadata, HashSet<string> queryTerms)
    {
        var bonus = 0.0;

        if (metadata.TryGetValue("section", out var section) && !string.IsNullOrEmpty(section))
        {
            var sectionTerms = Tokenize(section);
            if (queryTerms.Any(q => sectionTerms.Contains(q)))
                bonus += 0.1;
        }

        if (metadata.TryGetValue("tags", out var tags) && !string.IsNullOrEmpty(tags))
        {
            var tagTerms = Tokenize(tags);
            if (queryTerms.Any(q => tagTerms.Contains(q)))
                bonus += 0.05;
        }

        return bonus;
    }

    private static HashSet<string> Tokenize(string text)
    {
        return text.ToLowerInvariant()
            .Split(new[] { ' ', ',', '.', ';', ':', '!', '?', '\n', '\r', '\t', '/', '-', '_', '#' },
                StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 2)
            .ToHashSet();
    }
}
