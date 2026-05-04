using System.Diagnostics;
using System.Text;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Infrastructure.RAG;

/// <summary>
/// RAG Service — orchestrates: retrieve → rerank → build context.
/// Suporta múltiplas RetrievalStrategy (Default, RecentMemory, DomainKnowledge, etc.)
/// </summary>
public class RAGService : IRAGService
{
    private readonly IVectorStore _vectorStore;
    private readonly IReRanker _reRanker;
    private readonly ILogger<RAGService> _logger;

    public RAGService(IVectorStore vectorStore, IReRanker reRanker, ILogger<RAGService> logger)
    {
        _vectorStore = vectorStore;
        _reRanker = reRanker;
        _logger = logger;
    }

    public async Task<RAGContext> RetrieveContextAsync(RAGQuery query, CancellationToken ct = default)
    {
        var totalSw = Stopwatch.StartNew();

        // 1. Retrieve — busca vetorial com filtros opcionais baseados na strategy
        var retrievalSw = Stopwatch.StartNew();
        var filters = BuildFilters(query);
        SearchResult searchResult;

        if (filters.Count > 0)
        {
            searchResult = await _vectorStore.SearchWithFiltersAsync(query.Query, filters);
        }
        else
        {
            searchResult = await _vectorStore.SearchAsync(query.Query, query.Scope, query.MaxResults);
        }
        retrievalSw.Stop();

        if (searchResult.Matches.Count == 0)
        {
            totalSw.Stop();
            _logger.LogDebug("🔍 RAG: 0 results for query '{Query}'", TruncateQuery(query.Query));
            return new RAGContext
            {
                Query = query.Query,
                StrategyUsed = query.Strategy,
                TotalTime = totalSw.Elapsed,
                RetrievalTime = retrievalSw.Elapsed
            };
        }

        // 2. Filter by minimum score
        var aboveThreshold = searchResult.Matches
            .Where(m => m.Score >= query.MinRelevanceScore)
            .ToList();

        // 3. Re-rank
        var reRankSw = Stopwatch.StartNew();
        var ranked = await _reRanker.ReRankAsync(query.Query, aboveThreshold, query.TopKAfterReRank, ct);
        reRankSw.Stop();

        // 4. Build context string for prompt injection
        var context = BuildContextString(ranked);
        var totalTokens = EstimateTokens(context);

        totalSw.Stop();

        _logger.LogInformation(
            "🔍 RAG: {Retrieved}→{Filtered}→{ReRanked} chunks, {Tokens} tokens, strategy={Strategy}, {Ms}ms",
            searchResult.Matches.Count, aboveThreshold.Count, ranked.Count,
            totalTokens, query.Strategy, totalSw.ElapsedMilliseconds);

        return new RAGContext
        {
            Query = query.Query,
            Chunks = ranked.ToList(),
            BuiltContext = context,
            TotalTokensUsed = totalTokens,
            CandidatesRetrieved = searchResult.Matches.Count,
            CandidatesAfterReRank = ranked.Count,
            StrategyUsed = query.Strategy,
            RetrievalTime = retrievalSw.Elapsed,
            ReRankTime = reRankSw.Elapsed,
            TotalTime = totalSw.Elapsed
        };
    }

    private static Dictionary<string, string> BuildFilters(RAGQuery query)
    {
        var filters = query.Filters != null
            ? new Dictionary<string, string>(query.Filters)
            : new Dictionary<string, string>();

        switch (query.Strategy)
        {
            case RetrievalStrategy.DomainKnowledge:
                filters.TryAdd("content_type", "domain");
                break;
            case RetrievalStrategy.DecisionHistory:
                filters.TryAdd("content_type", "decision");
                break;
            case RetrievalStrategy.Episodic:
                filters.TryAdd("content_type", "session");
                break;
        }

        if (!string.IsNullOrEmpty(query.AgentId))
            filters.TryAdd("agent_id", query.AgentId);

        return filters;
    }

    private static string BuildContextString(IReadOnlyList<RankedChunk> chunks)
    {
        if (chunks.Count == 0) return string.Empty;

        var sb = new StringBuilder();
        sb.AppendLine("## Relevant Context");
        sb.AppendLine();

        foreach (var chunk in chunks)
        {
            var source = !string.IsNullOrEmpty(chunk.Source) ? $" (source: {chunk.Source})" : "";
            var section = !string.IsNullOrEmpty(chunk.Section) ? $" — {chunk.Section}" : "";

            sb.AppendLine($"### [{chunk.Rank}]{section}{source}");
            sb.AppendLine(chunk.Content);
            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }

    private static int EstimateTokens(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        return (int)Math.Ceiling(text.Length / 4.0);
    }

    private static string TruncateQuery(string query)
        => query.Length > 60 ? query[..60] + "..." : query;
}
