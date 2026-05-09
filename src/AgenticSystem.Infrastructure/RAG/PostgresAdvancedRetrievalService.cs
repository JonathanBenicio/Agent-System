using System.Text.Json;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Core.LLM.Interfaces;
using AgenticSystem.Infrastructure.Persistence;
using AgenticSystem.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.AI;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace AgenticSystem.Infrastructure.RAG;

public class PostgresAdvancedRetrievalService : IAdvancedRetrievalService
{
    private readonly IDbContextFactory<AgenticDbContext> _dbContextFactory;
    private readonly IEmbeddingProvider _embeddingProvider;
    private readonly IKnowledgeGraphService _graphService;
    private readonly IChatClient _chatClient;
    private readonly ILogger<PostgresAdvancedRetrievalService> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public PostgresAdvancedRetrievalService(
        IDbContextFactory<AgenticDbContext> dbContextFactory,
        IEmbeddingProvider embeddingProvider,
        IKnowledgeGraphService graphService,
        IChatClient chatClient,
        ILogger<PostgresAdvancedRetrievalService> logger)
    {
        _dbContextFactory = dbContextFactory;
        _embeddingProvider = embeddingProvider;
        _graphService = graphService;
        _chatClient = chatClient;
        _logger = logger;
    }

    public async Task<RAGContext> GraphSearchAsync(RAGQuery query, int maxDepth = 2, CancellationToken ct = default)
    {
        _logger.LogInformation("🕸️ Performing Graph Search for: {Query}", query.Query);
        var sw = System.Diagnostics.Stopwatch.StartNew();

        // 1. Initial vector/hybrid search to find entry points (entities)
        var initialContext = await HybridSearchAsync(query, null, ct);
        var entryChunks = initialContext.Chunks;

        if (entryChunks.Count == 0) return initialContext;

        // 2. Expand via knowledge graph
        var allChunks = new List<RankedChunk>(entryChunks);
        var seenIds = new HashSet<string>(entryChunks.Select(c => c.Id));

        foreach (var chunk in entryChunks)
        {
            // Find nodes associated with this chunk or content
            var relatedNodes = await _graphService.SearchNodesAsync(chunk.Content[..Math.Min(chunk.Content.Length, 100)], ct: ct);
            
            foreach (var node in relatedNodes)
            {
                var neighbors = await _graphService.GetNeighborsAsync(node.Id, ct: ct);
                foreach (var neighbor in neighbors)
                {
                    // If neighbor has a source document/chunk, add it
                    if (!string.IsNullOrEmpty(neighbor.SourceDocumentId) && seenIds.Add(neighbor.SourceDocumentId))
                    {
                        // Fetch the actual chunk content
                        var neighborChunk = await FetchChunkAsync(neighbor.SourceDocumentId, ct);
                        if (neighborChunk != null)
                        {
                            neighborChunk.ReRankedScore = chunk.ReRankedScore * 0.8; // Decay score for graph expansion
                            allChunks.Add(neighborChunk);
                        }
                    }
                }
            }
        }

        sw.Stop();
        _logger.LogInformation("🕸️ Graph expansion added {Count} related chunks in {Ms}ms", allChunks.Count - entryChunks.Count, sw.ElapsedMilliseconds);

        return new RAGContext
        {
            Query = query.Query,
            Chunks = allChunks.OrderByDescending(c => c.ReRankedScore).Take(query.MaxResults).ToList(),
            CandidatesRetrieved = allChunks.Count,
            TotalTime = sw.Elapsed
        };
    }

    private async Task<RankedChunk?> FetchChunkAsync(string id, CancellationToken ct)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var doc = await db.VectorDocuments.FindAsync(new object[] { id }, ct);
        if (doc == null) return null;

        return new RankedChunk
        {
            Id = doc.Id,
            Content = doc.Content,
            Metadata = JsonSerializer.Deserialize<Dictionary<string, string>>(doc.MetadataJson, JsonOptions) ?? new()
        };
    }

    public async Task<RAGContext> HybridSearchAsync(RAGQuery query, HybridSearchOptions? options = null, CancellationToken ct = default)
    {
        options ??= new HybridSearchOptions();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        float[] embeddingArray = await _embeddingProvider.GenerateEmbeddingAsync(query.Query, ct);
        var queryVector = new Vector(embeddingArray);

        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);

        var baseQuery = db.VectorDocuments.AsNoTracking();
        var collection = ScopeToCollection(query.Scope);
        if (!string.IsNullOrEmpty(collection))
        {
            baseQuery = baseQuery.Where(x => x.Collection == collection);
        }

        // === Vector Search (cosine similarity) ===
        var vectorResults = await baseQuery
            .OrderBy(x => x.Embedding!.CosineDistance(queryVector))
            .Take(query.MaxResults * 2)
            .ToListAsync(ct);

        // === Full-Text Search with BM25 ranking via ts_rank_cd ===
        // Sanitize query for tsquery: split into terms and join with '&'
        var tsQueryTerms = query.Query
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(t => t.Length > 1)
            .Select(t => t.Replace("'", "").Replace("\\", ""))
            .Where(t => !string.IsNullOrWhiteSpace(t));
        var tsQueryString = string.Join(" & ", tsQueryTerms);

        List<(VectorDocumentEntity Doc, float Rank)> ftsResults;

        if (!string.IsNullOrWhiteSpace(tsQueryString))
        {
            // Use raw SQL for ts_rank_cd which isn't directly exposed via EF Core LINQ
            ftsResults = await db.VectorDocuments
                .FromSqlInterpolated($"""
                    SELECT *, ts_rank_cd("SearchVector", plainto_tsquery('english', {query.Query})) AS rank_score
                    FROM vector_documents
                    WHERE "SearchVector" @@ plainto_tsquery('english', {query.Query})
                    ORDER BY rank_score DESC
                    LIMIT {query.MaxResults * 2}
                """)
                .AsNoTracking()
                .Select(x => new { Doc = x, Rank = 0f })
                .ToListAsync(ct)
                .ContinueWith(t => t.Result.Select(r => (r.Doc, r.Rank)).ToList(), ct);

            // If FTS returns no results, fallback to ILIKE for resilience
            if (ftsResults.Count == 0)
            {
                _logger.LogDebug("FTS returned 0 results, falling back to ILIKE for query: {Query}", query.Query);
                var ilikeFallback = await baseQuery
                    .Where(x => EF.Functions.ILike(x.Content, $"%{query.Query}%"))
                    .Take(query.MaxResults * 2)
                    .ToListAsync(ct);
                ftsResults = ilikeFallback.Select(d => (d, 0.5f)).ToList();
            }
        }
        else
        {
            ftsResults = [];
        }

        var keywordDocs = ftsResults.Select(r => r.Doc).ToList();

        // === RRF Merge ===
        var rrfScores = new Dictionary<string, double>();

        if (options.UseRRF)
        {
            CalculateRRF(vectorResults, rrfScores, options.RrfK, options.VectorWeight);
            CalculateRRF(keywordDocs, rrfScores, options.RrfK, options.KeywordWeight);
        }
        else
        {
            foreach (var v in vectorResults) rrfScores[v.Id] = rrfScores.GetValueOrDefault(v.Id) + options.VectorWeight;
            foreach (var k in keywordDocs) rrfScores[k.Id] = rrfScores.GetValueOrDefault(k.Id) + options.KeywordWeight;
        }

        var combinedDocs = vectorResults.Concat(keywordDocs).DistinctBy(x => x.Id).ToList();

        var rankedChunks = combinedDocs
            .Select(x => new RankedChunk
            {
                Id = x.Id,
                Content = x.Content,
                OriginalScore = rrfScores.GetValueOrDefault(x.Id, 0),
                ReRankedScore = rrfScores.GetValueOrDefault(x.Id, 0),
                Metadata = JsonSerializer.Deserialize<Dictionary<string, string>>(x.MetadataJson, JsonOptions) ?? new()
            })
            .OrderByDescending(x => x.ReRankedScore)
            .Take(query.MaxResults)
            .ToList();

        for (int i = 0; i < rankedChunks.Count; i++) rankedChunks[i].Rank = i + 1;

        sw.Stop();
        _logger.LogInformation(
            "HybridSearch completed: {VectorCount} vector + {FtsCount} FTS results merged to {FinalCount} in {Ms}ms",
            vectorResults.Count, keywordDocs.Count, rankedChunks.Count, sw.ElapsedMilliseconds);

        return new RAGContext
        {
            Query = query.Query,
            EffectiveQuery = query.Query,
            Chunks = rankedChunks,
            CandidatesRetrieved = combinedDocs.Count,
            TotalTime = sw.Elapsed
        };
    }

    private static void CalculateRRF(IEnumerable<VectorDocumentEntity> items, Dictionary<string, double> scores, int k, double weight)
    {
        int rank = 1;
        foreach (var item in items)
        {
            double rrfScore = weight * (1.0 / (k + rank));
            scores[item.Id] = scores.GetValueOrDefault(item.Id, 0) + rrfScore;
            rank++;
        }
    }

    public async Task<RAGContext> MultiQueryRetrieveAsync(RAGQuery query, CancellationToken ct = default)
    {
        _logger.LogInformation("Generating sub-queries for Multi-Query RAG...");
        
        var prompt = $"Rewrite the following user query into 3 distinct, varied sub-queries that capture different aspects of the original intent. Query: {query.Query}";
        
        var messages = new List<ChatMessage> { new(ChatRole.User, prompt) };
        var response = await _chatClient.GetResponseAsync(messages, cancellationToken: ct);
        var responseText = response.Text ?? string.Empty;
        
        var subQueries = responseText.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                                     .Select(q => q.Trim('-').Trim('*').Trim('1', '2', '3', '4', '5', '.', ' '))
                                     .Where(q => !string.IsNullOrWhiteSpace(q))
                                     .ToList();

        if (subQueries.Count == 0) subQueries.Add(query.Query);

        var allChunks = new List<RankedChunk>();
        
        foreach (var sq in subQueries)
        {
            var subQueryReq = new RAGQuery 
            { 
                Query = sq, 
                Scope = query.Scope, 
                MaxResults = query.MaxResults 
            };
            var result = await HybridSearchAsync(subQueryReq, null, ct);
            allChunks.AddRange(result.Chunks);
        }

        var distinctChunks = allChunks
            .GroupBy(c => c.Id)
            .Select(g => 
            {
                var chunk = g.First();
                chunk.ReRankedScore = g.Max(x => x.ReRankedScore);
                return chunk;
            })
            .OrderByDescending(c => c.ReRankedScore)
            .Take(query.MaxResults)
            .ToList();

        for (int i = 0; i < distinctChunks.Count; i++) distinctChunks[i].Rank = i + 1;

        return new RAGContext
        {
            Query = query.Query,
            EffectiveQuery = query.Query,
            QueryVariants = subQueries,
            Chunks = distinctChunks,
            CandidatesRetrieved = allChunks.Count
        };
    }

    public async Task<RAGContext> SelfCorrectiveRetrieveAsync(RAGQuery query, double relevanceThreshold = 0.5, int maxIterations = 3, CancellationToken ct = default)
    {
        var currentQuery = query.Query;
        RAGContext bestContext = null!;
        
        for (int i = 0; i < maxIterations; i++)
        {
            var iterationQuery = new RAGQuery { Query = currentQuery, Scope = query.Scope, MaxResults = query.MaxResults };
            var context = await HybridSearchAsync(iterationQuery, null, ct);
            
            var evalPrompt = $"Evaluate if these retrieved documents sufficiently answer the query. Query: '{query.Query}'. Documents: {string.Join(" ", context.Chunks.Select(c => c.Content))}. Output only a score between 0.0 and 1.0.";
            var evalMessages = new List<ChatMessage> { new(ChatRole.User, evalPrompt) };
            var evalResponse = await _chatClient.GetResponseAsync(evalMessages, cancellationToken: ct);
            
            if (double.TryParse(evalResponse.Text?.Trim(), out double score))
            {
                if (score >= relevanceThreshold)
                {
                    return context;
                }
            }

            bestContext = context; 
            
            var rewritePrompt = $"The query '{currentQuery}' did not return good results. Please rewrite the query to be more specific or use different keywords.";
            var rewriteMessages = new List<ChatMessage> { new(ChatRole.User, rewritePrompt) };
            var rewriteResponse = await _chatClient.GetResponseAsync(rewriteMessages, cancellationToken: ct);
            currentQuery = rewriteResponse.Text?.Trim() ?? currentQuery;
        }

        return bestContext;
    }

    public async Task<IReadOnlyList<RankedChunk>> ResolveParentChunksAsync(IReadOnlyList<RankedChunk> childChunks, CancellationToken ct = default)
    {
        var parentIds = childChunks
            .Where(c => c.Metadata.ContainsKey("parentId"))
            .Select(c => c.Metadata["parentId"])
            .Distinct()
            .ToList();

        if (parentIds.Count == 0) return childChunks;

        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var parentDocs = await db.VectorDocuments
            .Where(d => parentIds.Contains(d.Id))
            .ToListAsync(ct);

        var result = new List<RankedChunk>();
        foreach (var parent in parentDocs)
        {
            result.Add(new RankedChunk
            {
                Id = parent.Id,
                Content = parent.Content,
                Metadata = JsonSerializer.Deserialize<Dictionary<string, string>>(parent.MetadataJson, JsonOptions) ?? new()
            });
        }
        
        var chunksWithNoParent = childChunks.Where(c => !c.Metadata.ContainsKey("parentId"));
        result.AddRange(chunksWithNoParent);
        
        return result;
    }

    private static string ScopeToCollection(SearchScope scope) => scope switch
    {
        SearchScope.Notes => "notes",
        SearchScope.Agents => "agents",
        SearchScope.Decisions => "decisions",
        SearchScope.Domain => "domain",
        _ => ""
    };
}
