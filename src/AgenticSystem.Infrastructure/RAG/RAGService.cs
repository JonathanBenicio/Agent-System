using System.Diagnostics;
using System.Text;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using Microsoft.Extensions.AI;
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
    private readonly IAgentRuntimeCoordinator? _runtimeCoordinator;
    private readonly ILogger<RAGService> _logger;
    private readonly IKnowledgeFreshnessService? _freshnessService;
    private readonly IQueryCompressor? _queryCompressor;
    private readonly ISemanticCompressor? _semanticCompressor;
    private readonly IChatClient? _chatClient;

    public RAGService(
        IVectorStore vectorStore,
        IReRanker reRanker,
        ILogger<RAGService> logger,
        IAgentRuntimeCoordinator? runtimeCoordinator = null,
        IKnowledgeFreshnessService? freshnessService = null,
        IQueryCompressor? queryCompressor = null,
        ISemanticCompressor? semanticCompressor = null,
        IChatClient? chatClient = null)
    {
        _vectorStore = vectorStore;
        _reRanker = reRanker;
        _logger = logger;
        _runtimeCoordinator = runtimeCoordinator;
        _freshnessService = freshnessService;
        _queryCompressor = queryCompressor;
        _semanticCompressor = semanticCompressor;
        _chatClient = chatClient;
    }

    public async Task<RAGContext> RetrieveContextAsync(RAGQuery query, CancellationToken ct = default)
    {
        var totalSw = Stopwatch.StartNew();

        if (_runtimeCoordinator is not null)
        {
            await _runtimeCoordinator.PublishEventAsync(new AgentStreamEvent
            {
                Type = AgentStreamEventType.RagStarted,
                Message = query.Query,
                Data = new Dictionary<string, object>
                {
                    ["strategy"] = query.Strategy.ToString(),
                    ["scope"] = query.Scope.ToString()
                }
            }, ct);
        }

        // 1. Retrieve — busca vetorial com filtros opcionais baseados na strategy
        var retrievalSw = Stopwatch.StartNew();
        var filters = BuildFilters(query);

        // GAP-11 — Query Compression: otimiza query antes da busca vetorial
        CompressedQuery? compressedQuery = null;
        var queryVariants = new List<string> { query.Query };
        if (_queryCompressor != null)
        {
            try
            {
                compressedQuery = await _queryCompressor.CompressAsync(query.Query);
                if (!string.IsNullOrEmpty(compressedQuery.CompressedText))
                {
                    queryVariants = BuildQueryVariants(query.Query, compressedQuery);
                    _logger.LogDebug(
                        "🗜️ Query variants generated: {Original} → {Variants}",
                        TruncateQuery(query.Query),
                        string.Join(" | ", queryVariants.Select(TruncateQuery)));
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Query compression falhou, usando query original");
            }
        }

        var effectiveQuery = queryVariants[0];
        var searchResult = await SearchAcrossVariantsAsync(query, filters, queryVariants);

        string? hydeVariant = null;
        if (ShouldGenerateHydeVariant(query, searchResult))
        {
            hydeVariant = await GenerateHydeVariantAsync(query.Query, ct);
            if (!string.IsNullOrWhiteSpace(hydeVariant))
            {
                AddQueryVariant(queryVariants, hydeVariant);
                searchResult = await SearchAcrossVariantsAsync(query, filters, queryVariants);
                _logger.LogDebug(
                    "🧠 HyDE variant generated for '{Query}': {Variant}",
                    TruncateQuery(query.Query),
                    TruncateQuery(hydeVariant));
            }
        }

        retrievalSw.Stop();

        if (searchResult.Matches.Count == 0)
        {
            totalSw.Stop();
            _logger.LogDebug("🔍 RAG: 0 results for query '{Query}'", TruncateQuery(query.Query));
            return new RAGContext
            {
                Query = query.Query,
                EffectiveQuery = effectiveQuery,
                QueryVariants = queryVariants,
                UsedHydeExpansion = !string.IsNullOrWhiteSpace(hydeVariant),
                HydeVariant = hydeVariant,
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

        // 3.5 GAP-06 — Knowledge Freshness: penaliza chunks stale
        if (_freshnessService != null && ranked.Count > 0)
        {
            var rankedList = ranked.ToList();
            foreach (var chunk in rankedList)
            {
                try
                {
                    var freshnessScore = await _freshnessService.CalculateFreshnessScoreAsync(chunk.Id);
                    chunk.Metadata["freshnessScore"] = freshnessScore.ToString("F2");
                    if (freshnessScore < 0.5)
                    {
                        chunk.ReRankedScore *= freshnessScore;
                        chunk.Metadata["stalePenalized"] = "true";
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Freshness score não disponível para chunk {ChunkId}", chunk.Id);
                }
            }
            ranked = rankedList.OrderByDescending(c => c.ReRankedScore).ToList();
        }

        // 4. Build context string for prompt injection
        var originalContext = BuildContextString(ranked);
        var originalContextTokens = EstimateTokens(originalContext);
        var context = originalContext;
        var totalTokens = originalContextTokens;
        SemanticSummary? semanticSummary = null;
        var usedSemanticCompression = false;

        if (_semanticCompressor != null && ShouldCompressContext(ranked, originalContextTokens))
        {
            try
            {
                var compressedSummary = await _semanticCompressor.CompressRankedChunksAsync(ranked, query.Query);
                var promptChunks = ranked.Take(Math.Min(3, ranked.Count)).ToList();
                var compressedContext = BuildContextString(promptChunks, compressedSummary);
                var compressedContextTokens = EstimateTokens(compressedContext);

                if (compressedContextTokens < originalContextTokens)
                {
                    semanticSummary = compressedSummary;
                    context = compressedContext;
                    totalTokens = compressedContextTokens;
                    usedSemanticCompression = true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Semantic compression falhou, usando contexto completo");
            }
        }

        totalSw.Stop();

        _logger.LogInformation(
            "🔍 RAG: {Retrieved}→{Filtered}→{ReRanked} chunks, {Tokens} tokens, strategy={Strategy}, variants={VariantCount}, compressed={Compressed}, {Ms}ms",
            searchResult.Matches.Count, aboveThreshold.Count, ranked.Count,
            totalTokens, query.Strategy, queryVariants.Count, usedSemanticCompression, totalSw.ElapsedMilliseconds);

        if (_runtimeCoordinator is not null)
        {
            await _runtimeCoordinator.RecordArtifactAsync(new AgentExecutionArtifact
            {
                SessionId = _runtimeCoordinator.CurrentSessionId ?? string.Empty,
                Type = AgentExecutionArtifactType.RagContext,
                Name = $"RAG {query.Strategy}",
                AgentName = _runtimeCoordinator.CurrentAgentName,
                Status = ranked.Count > 0 ? "Found" : "Empty",
                Summary = query.Query,
                Data = new Dictionary<string, object>
                {
                    ["effectiveQuery"] = effectiveQuery,
                    ["queryVariants"] = queryVariants,
                    ["usedHydeExpansion"] = !string.IsNullOrWhiteSpace(hydeVariant),
                    ["hydeVariant"] = hydeVariant ?? string.Empty,
                    ["retrieved"] = searchResult.Matches.Count,
                    ["ranked"] = ranked.Count,
                    ["tokens"] = totalTokens,
                    ["originalTokens"] = originalContextTokens,
                    ["semanticCompression"] = usedSemanticCompression,
                    ["chunkIds"] = ranked.Select(chunk => chunk.Id).ToList(),
                    ["sources"] = ranked.Select(chunk => chunk.Source ?? string.Empty).Distinct().ToList()
                }
            }, ct);

            await _runtimeCoordinator.PublishEventAsync(new AgentStreamEvent
            {
                Type = AgentStreamEventType.RagCompleted,
                Message = query.Query,
                Data = new Dictionary<string, object>
                {
                    ["effectiveQuery"] = effectiveQuery,
                    ["queryVariants"] = queryVariants,
                    ["usedHydeExpansion"] = !string.IsNullOrWhiteSpace(hydeVariant),
                    ["retrieved"] = searchResult.Matches.Count,
                    ["ranked"] = ranked.Count,
                    ["latencyMs"] = totalSw.Elapsed.TotalMilliseconds,
                    ["tokens"] = totalTokens,
                    ["semanticCompression"] = usedSemanticCompression
                }
            }, ct);
        }

        return new RAGContext
        {
            Query = query.Query,
            EffectiveQuery = effectiveQuery,
            QueryVariants = queryVariants,
            UsedHydeExpansion = !string.IsNullOrWhiteSpace(hydeVariant),
            HydeVariant = hydeVariant,
            Chunks = ranked.ToList(),
            BuiltContext = context,
            SemanticSummary = semanticSummary?.CompressedKnowledge,
            UsedSemanticCompression = usedSemanticCompression,
            OriginalContextTokens = originalContextTokens,
            TotalTokensUsed = totalTokens,
            CandidatesRetrieved = searchResult.Matches.Count,
            CandidatesAfterReRank = ranked.Count,
            StrategyUsed = query.Strategy,
            RetrievalTime = retrievalSw.Elapsed,
            ReRankTime = reRankSw.Elapsed,
            TotalTime = totalSw.Elapsed
        };
    }

    private async Task<SearchResult> SearchAcrossVariantsAsync(
        RAGQuery query,
        Dictionary<string, string> filters,
        IReadOnlyList<string> queryVariants)
    {
        if (queryVariants.Count == 1)
        {
            return filters.Count > 0
                ? await _vectorStore.SearchWithFiltersAsync(queryVariants[0], filters)
                : await _vectorStore.SearchAsync(queryVariants[0], query.Scope, query.MaxResults);
        }

        var mergedMatches = new Dictionary<string, SearchMatch>(StringComparer.OrdinalIgnoreCase);

        foreach (var variant in queryVariants)
        {
            var variantTag = ToVariantTag(variant);
            var variantResult = filters.Count > 0
                ? await _vectorStore.SearchWithFiltersAsync(variant, filters)
                : await _vectorStore.SearchAsync(variant, query.Scope, query.MaxResults);

            foreach (var match in variantResult.Matches)
            {
                if (mergedMatches.TryGetValue(match.Id, out var existing))
                {
                    if (match.Score > existing.Score)
                    {
                        existing.Content = match.Content;
                        existing.Type = match.Type;
                        existing.Score = match.Score;
                        existing.Snippet = match.Snippet;
                    }

                    existing.Metadata["matched_query_variants"] = MergeVariantMetadata(
                        existing.Metadata.GetValueOrDefault("matched_query_variants"),
                        variantTag);
                    continue;
                }

                var metadata = new Dictionary<string, string>(match.Metadata, StringComparer.OrdinalIgnoreCase)
                {
                    ["matched_query_variants"] = variantTag
                };

                mergedMatches[match.Id] = new SearchMatch
                {
                    Id = match.Id,
                    Content = match.Content,
                    Type = match.Type,
                    Score = match.Score,
                    Metadata = metadata,
                    Snippet = match.Snippet
                };
            }
        }

        return new SearchResult
        {
            Query = queryVariants[0],
            Scope = query.Scope,
            TotalFound = mergedMatches.Count,
            Matches = mergedMatches.Values
                .OrderByDescending(match => match.Score)
                .ToList()
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

    private static List<string> BuildQueryVariants(string originalQuery, CompressedQuery compressedQuery)
    {
        var variants = new List<string>();

        AddQueryVariant(variants, compressedQuery.CompressedText);

        if (compressedQuery.ExtractedKeyTerms.Count > 1)
        {
            AddQueryVariant(variants, string.Join(" ", compressedQuery.ExtractedKeyTerms.Take(8)));
        }

        AddQueryVariant(variants, originalQuery);
        return variants;
    }

    private static void AddQueryVariant(List<string> variants, string candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return;
        }

        var normalized = string.Join(' ', candidate
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        if (!variants.Contains(normalized, StringComparer.OrdinalIgnoreCase))
        {
            variants.Add(normalized);
        }
    }

    private static string BuildContextString(IReadOnlyList<RankedChunk> chunks, SemanticSummary? semanticSummary = null)
    {
        if (chunks.Count == 0 && semanticSummary is null) return string.Empty;

        var sb = new StringBuilder();
        sb.AppendLine("## Relevant Context");
        sb.AppendLine();

        if (semanticSummary is not null)
        {
            sb.AppendLine("### Semantic Summary");
            sb.AppendLine(semanticSummary.CompressedKnowledge);
            sb.AppendLine();
        }

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

    /// <summary>
    /// Average chars per token for GPT-class models (~4 for English, ~3 for multilingual).
    /// </summary>
    private const double CharsPerToken = 3.5;

    private static bool ShouldCompressContext(IReadOnlyList<RankedChunk> chunks, int originalContextTokens)
        => chunks.Count > 3 || originalContextTokens > 800;

    private bool ShouldGenerateHydeVariant(RAGQuery query, SearchResult searchResult)
    {
        if (_chatClient is null)
        {
            return false;
        }

        if (query.Strategy is RetrievalStrategy.RecentMemory or RetrievalStrategy.Episodic)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(query.Query) || query.Query.Length < 12)
        {
            return false;
        }

        return searchResult.Matches.Count < Math.Max(query.TopKAfterReRank, 3);
    }

    private async Task<string?> GenerateHydeVariantAsync(string originalQuery, CancellationToken cancellationToken)
    {
        if (_chatClient is null)
        {
            return null;
        }

        try
        {
            var response = await _chatClient.GetResponseAsync(
                new[]
                {
                    new ChatMessage(
                        ChatRole.System,
                        "You expand retrieval queries. Write a short hypothetical passage that is likely to appear in a relevant document. Use the same language as the user. Do not answer the user directly. Do not use markdown or bullet points."),
                    new ChatMessage(ChatRole.User, originalQuery)
                },
                new ChatOptions
                {
                    Temperature = 0.2f,
                    MaxOutputTokens = 160
                },
                cancellationToken);

            var candidate = response.Text.Trim();
            return string.IsNullOrWhiteSpace(candidate) ? null : candidate;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "HyDE generation failed for query '{Query}'", TruncateQuery(originalQuery));
            return null;
        }
    }

    private static string MergeVariantMetadata(string? currentValue, string variant)
    {
        var values = (currentValue ?? string.Empty)
            .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        values.Add(variant);
        return string.Join(" | ", values);
    }

    private static int EstimateTokens(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        return (int)Math.Ceiling(text.Length / CharsPerToken);
    }

    private static string ToVariantTag(string variant)
        => variant.Length > 80 ? variant[..80] + "..." : variant;

    private static string TruncateQuery(string query)
        => query.Length > 60 ? query[..60] + "..." : query;
}
