using System.Globalization;
using System.Text;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.Configuration;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using TextEmbeddingGenerator = Microsoft.Extensions.AI.IEmbeddingGenerator<string, Microsoft.Extensions.AI.Embedding<float>>;

namespace AgenticSystem.Infrastructure.RAG;

/// <summary>
/// Re-ranker híbrido: usa heurística como shortlist/fallback e aplica LLM-as-ReRanker
/// apenas quando a ordenação inicial está ambígua.
/// </summary>
public sealed class LlmReRanker : IReRanker
{
    private readonly HeuristicReRanker _heuristicReRanker;
    private readonly IChatClient _chatClient;
    private readonly IReadOnlyList<IDedicatedReRankerProvider> _dedicatedProviders;
    private readonly TextEmbeddingGenerator? _embeddingGenerator;
    private readonly IRerankingSettingsAccessor _settingsAccessor;
    private readonly ILogger<LlmReRanker> _logger;

    public LlmReRanker(
        HeuristicReRanker heuristicReRanker,
        IChatClient chatClient,
        IEnumerable<IDedicatedReRankerProvider> dedicatedProviders,
        TextEmbeddingGenerator? embeddingGenerator,
        IRerankingSettingsAccessor settingsAccessor,
        ILogger<LlmReRanker> logger)
    {
        _heuristicReRanker = heuristicReRanker;
        _chatClient = chatClient;
        _dedicatedProviders = dedicatedProviders.ToList();
        _embeddingGenerator = embeddingGenerator;
        _settingsAccessor = settingsAccessor;
        _logger = logger;
    }

    public async Task<IReadOnlyList<RankedChunk>> ReRankAsync(
        string query,
        IReadOnlyList<SearchMatch> candidates,
        int topK = 5,
        CancellationToken ct = default)
    {
        var options = await _settingsAccessor.GetCurrentOptionsAsync(ct);
        var candidatePoolSize = Math.Max(topK, options.CandidatePoolSize);
        var heuristicRanked = await _heuristicReRanker.ReRankAsync(query, candidates, candidatePoolSize, ct);

        if (!ShouldUseLlmReRanking(heuristicRanked, options))
        {
            return heuristicRanked.Take(topK).ToList();
        }

        try
        {
            var scoringResult = await ScoreWithDedicatedProviderAsync(query, heuristicRanked, options, ct);
            if (scoringResult.Scores.Count == 0)
            {
                scoringResult = await ScoreWithEmbeddingAsync(query, heuristicRanked, options, ct);
            }

            if (scoringResult.Scores.Count == 0)
            {
                scoringResult = await ScoreWithLlmAsync(query, heuristicRanked, options, ct);
            }

            if (scoringResult.Scores.Count == 0)
            {
                return heuristicRanked.Take(topK).ToList();
            }

            var configuredWeight = options.NeuralScoreWeight > 0.0
                ? options.NeuralScoreWeight
                : options.LlmScoreWeight;
            var neuralWeight = Math.Clamp(configuredWeight, 0.0, 1.0);
            var heuristicWeight = 1.0 - neuralWeight;

            var reranked = heuristicRanked
                .Select(chunk =>
                {
                    var neuralScore = scoringResult.Scores.GetValueOrDefault(chunk.Id, chunk.ReRankedScore);
                    chunk.ReRankedScore = Math.Clamp(chunk.ReRankedScore * heuristicWeight + neuralScore * neuralWeight, 0.0, 1.0);
                    chunk.Metadata["reranker"] = scoringResult.ProviderName;
                    chunk.Metadata["neural_rerank_score"] = neuralScore.ToString("F3", CultureInfo.InvariantCulture);
                    chunk.Metadata["neural_rerank_provider"] = scoringResult.ProviderName;
                    return chunk;
                })
                .OrderByDescending(chunk => chunk.ReRankedScore)
                .Take(topK)
                .Select((chunk, index) =>
                {
                    chunk.Rank = index + 1;
                    return chunk;
                })
                .ToList();

            _logger.LogDebug(
                "🧠 Neural re-ranked {Count} shortlisted candidates for query {Query} using {Provider}",
                heuristicRanked.Count,
                query.Length > 60 ? query[..60] + "..." : query,
                scoringResult.ProviderName);

            return reranked;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "LLM re-ranking falhou, usando ranking heurístico para '{Query}'", query);
            return heuristicRanked.Take(topK).ToList();
        }
    }

    private static bool ShouldUseLlmReRanking(IReadOnlyList<RankedChunk> heuristicRanked, ReRankingOptions options)
    {
        if (!options.Enabled || (!options.UseDedicatedProvider && !options.UseEmbeddingReRanking && !options.UseLlmReRanking))
        {
            return false;
        }

        if (heuristicRanked.Count < Math.Max(2, options.MinCandidateCountForLlm))
        {
            return false;
        }

        var top = heuristicRanked[0].ReRankedScore;
        var second = heuristicRanked[1].ReRankedScore;
        var confidentGap = top - second;

        return !(top >= options.HeuristicConfidenceThreshold && confidentGap >= options.HeuristicConfidenceGap);
    }

    private async Task<NeuralScoringResult> ScoreWithDedicatedProviderAsync(
        string query,
        IReadOnlyList<RankedChunk> candidates,
        ReRankingOptions options,
        CancellationToken ct)
    {
        var providerResult = await ScoreWithSpecializedProviderAsync(query, candidates, options, ct);
        if (providerResult.Scores.Count > 0)
        {
            return providerResult;
        }

        return NeuralScoringResult.Empty;
    }

    private async Task<NeuralScoringResult> ScoreWithEmbeddingAsync(
        string query,
        IReadOnlyList<RankedChunk> candidates,
        ReRankingOptions options,
        CancellationToken ct)
    {
        if (!options.UseEmbeddingReRanking || _embeddingGenerator is null)
        {
            return NeuralScoringResult.Empty;
        }

        try
        {
            var inputs = new List<string>(candidates.Count + 1) { query };
            inputs.AddRange(candidates.Select(candidate => BuildCandidateText(candidate, options)));

            var embeddings = await _embeddingGenerator.GenerateAsync(inputs, cancellationToken: ct);
            var vectors = embeddings.Select(item => item.Vector.ToArray()).ToList();
            if (vectors.Count != inputs.Count)
            {
                return NeuralScoringResult.Empty;
            }

            var queryVector = vectors[0];
            var scores = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < candidates.Count; i++)
            {
                var cosine = CosineSimilarity(queryVector, vectors[i + 1]);
                scores[candidates[i].Id] = (cosine + 1.0) / 2.0;
            }

            return new NeuralScoringResult(scores, "embedding-hybrid");
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Embedding re-ranking falhou, tentando fallback LLM para '{Query}'", query);
            return NeuralScoringResult.Empty;
        }
    }

    private async Task<NeuralScoringResult> ScoreWithSpecializedProviderAsync(
        string query,
        IReadOnlyList<RankedChunk> candidates,
        ReRankingOptions options,
        CancellationToken ct)
    {
        if (!options.UseDedicatedProvider || _dedicatedProviders.Count == 0)
        {
            return NeuralScoringResult.Empty;
        }

        var provider = _dedicatedProviders.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, options.DedicatedProvider, StringComparison.OrdinalIgnoreCase));

        if (provider is null)
        {
            provider = _dedicatedProviders[0];
        }

        var result = await provider.ScoreAsync(query, candidates, ct);
        return result.Scores.Count == 0
            ? NeuralScoringResult.Empty
            : new NeuralScoringResult(new Dictionary<string, double>(result.Scores, StringComparer.OrdinalIgnoreCase), result.ProviderName);
    }

    private async Task<NeuralScoringResult> ScoreWithLlmAsync(
        string query,
        IReadOnlyList<RankedChunk> candidates,
        ReRankingOptions options,
        CancellationToken ct)
    {
        if (!options.UseLlmReRanking)
        {
            return NeuralScoringResult.Empty;
        }

        var prompt = BuildPrompt(query, candidates, options);
        var response = await _chatClient.GetResponseAsync(
            new[]
            {
                new ChatMessage(
                    ChatRole.System,
                    "You rerank retrieval snippets for RAG. Return exactly one line per candidate using the format index|score where score is a number from 0.00 to 1.00. Use descending relevance. No markdown. No explanations."),
                new ChatMessage(ChatRole.User, prompt)
            },
            new ChatOptions
            {
                Temperature = options.Temperature,
                MaxOutputTokens = options.MaxOutputTokens
            },
            ct);

        var scores = ParseScores(response.Text, candidates);
        return scores.Count == 0
            ? NeuralScoringResult.Empty
            : new NeuralScoringResult(scores, "llm-hybrid");
    }

    private static string BuildPrompt(string query, IReadOnlyList<RankedChunk> candidates, ReRankingOptions options)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Query:");
        sb.AppendLine(query.Trim());
        sb.AppendLine();
        sb.AppendLine("Candidates:");

        for (var i = 0; i < candidates.Count; i++)
        {
            var candidate = candidates[i];
            var snippet = TruncateSnippet(candidate.Content, options);
            sb.Append(i + 1);
            sb.Append(". [");
            sb.Append(candidate.Id);
            sb.Append("] ");

            if (!string.IsNullOrWhiteSpace(candidate.Section))
            {
                sb.Append(candidate.Section);
                sb.Append(" | ");
            }

            sb.AppendLine(snippet);
        }

        return sb.ToString();
    }

    private Dictionary<string, double> ParseScores(string responseText, IReadOnlyList<RankedChunk> candidates)
    {
        var scores = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(responseText))
        {
            return scores;
        }

        var indexToId = candidates
            .Select((candidate, index) => new { Index = index + 1, candidate.Id })
            .ToDictionary(item => item.Index, item => item.Id);

        foreach (var rawLine in responseText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = rawLine.Split('|', StringSplitOptions.TrimEntries);
            if (parts.Length < 2)
            {
                continue;
            }

            if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var index))
            {
                continue;
            }

            if (!double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var score))
            {
                continue;
            }

            if (!indexToId.TryGetValue(index, out var id))
            {
                continue;
            }

            scores[id] = Math.Clamp(score, 0.0, 1.0);
        }

        return scores;
    }

    private static string TruncateSnippet(string content, ReRankingOptions options)
    {
        var normalized = string.Join(' ', content
            .Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        var maxSnippetCharacters = options.MaxSnippetCharacters;
        if (normalized.Length <= maxSnippetCharacters)
        {
            return normalized;
        }

        return normalized[..maxSnippetCharacters] + "...";
    }

    private static string BuildCandidateText(RankedChunk candidate, ReRankingOptions options)
    {
        var snippet = TruncateSnippet(candidate.Content, options);
        if (string.IsNullOrWhiteSpace(candidate.Section))
        {
            return snippet;
        }

        return $"{candidate.Section}: {snippet}";
    }

    private static double CosineSimilarity(float[] left, float[] right)
    {
        if (left.Length != right.Length)
        {
            return 0.0;
        }

        double dot = 0.0;
        double leftNorm = 0.0;
        double rightNorm = 0.0;
        for (var index = 0; index < left.Length; index++)
        {
            dot += left[index] * (double)right[index];
            leftNorm += left[index] * (double)left[index];
            rightNorm += right[index] * (double)right[index];
        }

        var denominator = Math.Sqrt(leftNorm) * Math.Sqrt(rightNorm);
        return denominator == 0.0 ? 0.0 : dot / denominator;
    }

    private sealed record NeuralScoringResult(Dictionary<string, double> Scores, string ProviderName)
    {
        public static NeuralScoringResult Empty { get; } = new(new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase), "heuristic-only");
    }
}