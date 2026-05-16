using System.Text.Json;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Pgvector.EntityFrameworkCore;

namespace AgenticSystem.Infrastructure.Persistence;

/// <summary>
/// Implementação PostgreSQL de IVectorStore com busca vetorial nativa via pgvector.
/// Utiliza tabela vector_documents com similaridade do cosseno SQL e fallback para busca textual/ONNX.
/// </summary>
public class PostgresVectorStore : IVectorStore
{
    private readonly IDbContextFactory<AgenticDbContext> _dbContextFactory;
    private readonly ILogger<PostgresVectorStore> _logger;
    private readonly IEmbeddingGenerator<string, Embedding<float>>? _embeddingGenerator;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public PostgresVectorStore(
        IDbContextFactory<AgenticDbContext> dbContextFactory,
        ILogger<PostgresVectorStore> logger,
        IEmbeddingGenerator<string, Embedding<float>>? embeddingGenerator = null)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
        _embeddingGenerator = embeddingGenerator;
    }

    public async Task UpsertAsync(EmbeddingDocument document)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        var entity = await db.VectorDocuments.FirstOrDefaultAsync(item => item.Id == document.Id);
        if (entity is null)
        {
            db.VectorDocuments.Add(MapToEntity(document));
        }
        else
        {
            entity.Content = document.Content;
            entity.Type = document.Type;
            entity.Collection = document.Collection;
            entity.Embedding = document.Embedding != null ? new Pgvector.Vector(document.Embedding) : null;
            entity.MetadataJson = JsonSerializer.Serialize(document.Metadata, JsonOptions);
            entity.ContextualSummary = document.ContextualSummary;
            entity.IndexedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();
        _logger.LogDebug("Upserted document {Id} in collection {Collection} to PostgreSQL via EF Core", document.Id, document.Collection);
    }

    public async Task DeleteAsync(string id, string? collection = null)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        var query = db.VectorDocuments.Where(item => item.Id == id);
        if (!string.IsNullOrWhiteSpace(collection))
        {
            query = query.Where(item => item.Collection == collection);
        }

        var entities = await query.ToListAsync();
        if (entities.Count == 0)
        {
            return;
        }

        db.VectorDocuments.RemoveRange(entities);
        await db.SaveChangesAsync();
        _logger.LogDebug("Deleted {Count} document(s) with id {Id} from collection {Collection}", entities.Count, id, collection ?? "*");
    }

    public async Task<SearchResult> SearchAsync(string query, SearchScope scope = SearchScope.All, int maxResults = 10)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        await using var db = await _dbContextFactory.CreateDbContextAsync();
        var collection = scope != SearchScope.All ? ScopeToCollection(scope) : null;

        float[]? queryEmbedding = null;
        if (!string.IsNullOrWhiteSpace(query) && _embeddingGenerator != null)
        {
            try
            {
                var generated = await _embeddingGenerator.GenerateAsync(query);
                queryEmbedding = generated.Vector.ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to generate embedding for query: {Query}. Using FTS-only fallback.", query);
            }
        }

        var candidatesQuery = db.VectorDocuments.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(collection))
        {
            candidatesQuery = candidatesQuery.Where(item => item.Collection == collection);
        }

        // Use hybrid search when embedding is available, otherwise FTS-only
        if (queryEmbedding != null)
        {
            return await HybridSearchAsync(db, candidatesQuery, query, queryEmbedding, maxResults, scope, sw);
        }

        // FTS-only fallback
        return await FtsOnlySearchAsync(db, candidatesQuery, query, maxResults, scope, sw);
    }

    /// <summary>
    /// Hybrid Search: combines pgvector cosine similarity with PostgreSQL Full-Text Search (ts_rank).
    /// Uses Reciprocal Rank Fusion (RRF) to merge rankings from both signals.
    /// Part of "Enterprise Scale" (Phase 4).
    /// </summary>
    private async Task<SearchResult> HybridSearchAsync(
        AgenticDbContext db,
        IQueryable<VectorDocumentEntity> baseQuery,
        string query,
        float[] queryEmbedding,
        int maxResults,
        SearchScope scope,
        System.Diagnostics.Stopwatch sw)
    {
        const int candidatePool = 50;
        const double rrf_k = 60.0; // RRF constant — standard value from the original paper

        var vector = new Pgvector.Vector(queryEmbedding);

        // 1. Vector search — top candidates by cosine distance
        var vectorCandidates = await baseQuery
            .Where(item => item.Embedding != null)
            .OrderBy(item => item.Embedding!.CosineDistance(vector))
            .Take(candidatePool)
            .ToListAsync();

        // 2. FTS search — top candidates by ts_rank (raw SQL for to_tsvector/plainto_tsquery)
        var ftsCandidates = await baseQuery
            .Where(item => EF.Functions.ToTsVector("english", item.Content)
                .Matches(EF.Functions.PlainToTsQuery("english", query)))
            .OrderByDescending(item => EF.Functions.ToTsVector("english", item.Content)
                .Rank(EF.Functions.PlainToTsQuery("english", query)))
            .Take(candidatePool)
            .ToListAsync();

        // 3. Reciprocal Rank Fusion (RRF) — merge both rankings
        var vectorRanks = vectorCandidates
            .Select((item, rank) => (item.Id, Rank: rank + 1))
            .ToDictionary(x => x.Id, x => x.Rank);

        var ftsRanks = ftsCandidates
            .Select((item, rank) => (item.Id, Rank: rank + 1))
            .ToDictionary(x => x.Id, x => x.Rank);

        var allIds = vectorRanks.Keys.Union(ftsRanks.Keys).ToHashSet();
        var allEntities = vectorCandidates
            .Concat(ftsCandidates)
            .DistinctBy(x => x.Id)
            .ToDictionary(x => x.Id);

        var fusedResults = allIds.Select(id =>
        {
            var vectorScore = vectorRanks.TryGetValue(id, out var vr) ? 1.0 / (rrf_k + vr) : 0.0;
            var ftsScore = ftsRanks.TryGetValue(id, out var fr) ? 1.0 / (rrf_k + fr) : 0.0;
            var rrfScore = vectorScore + ftsScore;
            return (Id: id, RrfScore: rrfScore, Entity: allEntities[id]);
        })
        .OrderByDescending(x => x.RrfScore)
        .Take(maxResults)
        .ToList();

        var matches = fusedResults
            .Select(x => MapToSearchMatch(x.Entity, x.RrfScore))
            .ToList();

        sw.Stop();
        _logger.LogDebug("🔍 Hybrid Search: {VectorCount} vector + {FtsCount} FTS → {MergedCount} fused results in {Elapsed}ms",
            vectorCandidates.Count, ftsCandidates.Count, matches.Count, sw.ElapsedMilliseconds);

        return new SearchResult
        {
            Query = query,
            Scope = scope,
            Matches = matches,
            TotalFound = matches.Count,
            ExecutionTime = sw.Elapsed
        };
    }

    /// <summary>
    /// Full-Text Search only fallback when embedding generation is unavailable.
    /// </summary>
    private async Task<SearchResult> FtsOnlySearchAsync(
        AgenticDbContext db,
        IQueryable<VectorDocumentEntity> baseQuery,
        string query,
        int maxResults,
        SearchScope scope,
        System.Diagnostics.Stopwatch sw)
    {
        if (!string.IsNullOrWhiteSpace(query))
        {
            // Try FTS first, fall back to ILIKE if query doesn't parse well
            try
            {
                var ftsQuery = baseQuery
                    .Where(item => EF.Functions.ToTsVector("english", item.Content)
                        .Matches(EF.Functions.PlainToTsQuery("english", query)));

                var candidates = await ftsQuery
                    .OrderByDescending(item => EF.Functions.ToTsVector("english", item.Content)
                        .Rank(EF.Functions.PlainToTsQuery("english", query)))
                    .Take(maxResults)
                    .ToListAsync();

                if (candidates.Count > 0)
                {
                    var matches = candidates
                        .Select((item, idx) => MapToSearchMatch(item, 1.0 / (60.0 + idx + 1)))
                        .ToList();

                    sw.Stop();
                    return new SearchResult
                    {
                        Query = query,
                        Scope = scope,
                        Matches = matches,
                        TotalFound = matches.Count,
                        ExecutionTime = sw.Elapsed
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "FTS query failed, falling back to ILIKE for: {Query}", query);
            }

            // ILIKE fallback
            baseQuery = baseQuery.Where(item => EF.Functions.ILike(item.Content, $"%{query}%"));
        }

        var fallbackCandidates = await baseQuery
            .OrderByDescending(item => item.IndexedAt)
            .Take(Math.Max(maxResults * 5, maxResults))
            .ToListAsync();

        var fallbackMatches = fallbackCandidates
            .Select(item => MapToSearchMatch(item, ScoreContent(item.Content, query)))
            .OrderByDescending(item => item.Score)
            .Take(maxResults)
            .ToList();

        sw.Stop();

        return new SearchResult
        {
            Query = query,
            Scope = scope,
            Matches = fallbackMatches,
            TotalFound = fallbackMatches.Count,
            ExecutionTime = sw.Elapsed
        };
    }

    public async Task<SearchResult> SearchWithFiltersAsync(string query, Dictionary<string, string> filters)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var normalizedQuery = query.Trim();
        var hasQuery = !string.IsNullOrWhiteSpace(normalizedQuery) && normalizedQuery != "*";

        await using var db = await _dbContextFactory.CreateDbContextAsync();
        var dataQuery = db.VectorDocuments.AsNoTracking().AsQueryable();

        if (filters.TryGetValue("type", out var typeFilter))
        {
            dataQuery = dataQuery.Where(item => item.Type == typeFilter);
        }

        if (filters.TryGetValue("collection", out var collectionFilter))
        {
            dataQuery = dataQuery.Where(item => item.Collection == collectionFilter);
        }

        if (filters.TryGetValue("id", out var idFilter))
        {
            dataQuery = dataQuery.Where(item => item.Id == idFilter);
        }

        float[]? queryEmbedding = null;
        if (hasQuery && _embeddingGenerator != null)
        {
            try
            {
                var generated = await _embeddingGenerator.GenerateAsync(normalizedQuery);
                queryEmbedding = generated.Vector.ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha ao gerar embedding para query em filtros: {Query}. Utilizando fallback textual/ONNX.", normalizedQuery);
            }
        }

        List<VectorDocumentEntity> candidates;
        if (queryEmbedding != null)
        {
            var vector = new Pgvector.Vector(queryEmbedding);
            candidates = await dataQuery
                .Where(item => item.Embedding != null)
                .OrderBy(item => item.Embedding!.CosineDistance(vector))
                .Take(50)
                .ToListAsync();
        }
        else
        {
            if (hasQuery)
            {
                dataQuery = dataQuery.Where(item => EF.Functions.ILike(item.Content, $"%{normalizedQuery}%"));
            }

            candidates = await dataQuery
                .OrderByDescending(item => item.IndexedAt)
                .Take(hasQuery ? 50 : 200)
                .ToListAsync();
        }

        var remainingFilters = filters
            .Where(item => item.Key is not ("type" or "collection" or "id"))
            .ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase);

        var filtered = candidates.Where(item => MetadataMatches(item.MetadataJson, remainingFilters));

        List<SearchMatch> matches;
        if (queryEmbedding != null)
        {
            matches = filtered
                .Select(item => MapToSearchMatch(item, 1d - (item.Embedding != null ? CalculateCosineDistanceLocal(item.Embedding.ToArray(), queryEmbedding) : 0d)))
                .OrderByDescending(item => item.Score)
                .Take(10)
                .ToList();
        }
        else
        {
            matches = filtered
                .Select(item => MapToSearchMatch(item, hasQuery ? ScoreContent(item.Content, normalizedQuery) : 1d))
                .OrderByDescending(item => item.Score)
                .ThenByDescending(item => item.IndexedAt)
                .Take(10)
                .ToList();
        }

        sw.Stop();

        return new SearchResult
        {
            Query = query,
            Matches = matches,
            TotalFound = matches.Count,
            ExecutionTime = sw.Elapsed
        };
    }

    public async Task<IEnumerable<string>> GetCollectionsAsync()
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        return await db.VectorDocuments
            .AsNoTracking()
            .Select(item => item.Collection)
            .Distinct()
            .OrderBy(item => item)
            .ToListAsync();
    }

    public async Task CleanupOldDocumentsAsync(TimeSpan olderThan)
    {
        var cutoff = DateTime.UtcNow - olderThan;
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        var entities = await db.VectorDocuments.Where(item => item.IndexedAt < cutoff).ToListAsync();
        var deleted = entities.Count;
        if (deleted > 0)
        {
            db.VectorDocuments.RemoveRange(entities);
            await db.SaveChangesAsync();
        }

        _logger.LogInformation("Cleaned up {Count} old documents from PostgreSQL (older than {Days} days)",
            deleted, olderThan.TotalDays);
    }

    public async Task<VectorStoreStats> GetStatsAsync(string tenantId, CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var stats = await db.VectorDocuments
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .GroupBy(x => x.TenantId)
            .Select(g => new VectorStoreStats
            {
                TenantId = g.Key,
                DocumentCount = g.Count(),
                // Simplification for Postgres: length of text and embeddings roughly calculated
                TotalBytes = g.Sum(x => x.Content.Length * 2 + (x.EmbeddingData != null ? x.EmbeddingData.Length : 0))
            })
            .FirstOrDefaultAsync(ct);

        return stats ?? new VectorStoreStats { TenantId = tenantId, DocumentCount = 0, TotalBytes = 0 };
    }

    private static EmbeddingDocument MapToModel(VectorDocumentEntity entity)
    {
        return new EmbeddingDocument
        {
            Id = entity.Id,
            Content = entity.Content,
            Type = entity.Type,
            Collection = entity.Collection,
            Embedding = entity.Embedding?.ToArray() ?? Array.Empty<float>(),
            Metadata = JsonSerializer.Deserialize<Dictionary<string, string>>(entity.MetadataJson, JsonOptions) ?? new(),
            ContextualSummary = entity.ContextualSummary,
            IndexedAt = entity.IndexedAt
        };
    }

    private static VectorDocumentEntity MapToEntity(EmbeddingDocument document)
    {
        return new VectorDocumentEntity
        {
            Id = document.Id,
            Content = document.Content,
            Type = document.Type,
            Collection = document.Collection,
            Embedding = document.Embedding != null ? new Pgvector.Vector(document.Embedding) : null,
            MetadataJson = JsonSerializer.Serialize(document.Metadata, JsonOptions),
            ContextualSummary = document.ContextualSummary,
            IndexedAt = DateTime.UtcNow
        };
    }

    private static SearchMatch MapToSearchMatch(VectorDocumentEntity entity, double score)
    {
        var model = MapToModel(entity);
        return new SearchMatch
        {
            Id = model.Id,
            Content = model.Content,
            Type = model.Type,
            Collection = model.Collection,
            Score = score,
            Metadata = model.Metadata,
            Snippet = model.Content.Length > 300 ? model.Content[..300] + "..." : model.Content,
            Embedding = model.Embedding,
            ContextualSummary = model.ContextualSummary,
            IndexedAt = model.IndexedAt
        };
    }

    private static bool MetadataMatches(string metadataJson, IReadOnlyDictionary<string, string> filters)
    {
        if (filters.Count == 0)
        {
            return true;
        }

        var metadata = JsonSerializer.Deserialize<Dictionary<string, string>>(metadataJson, JsonOptions) ?? new();
        return filters.All(filter => metadata.TryGetValue(filter.Key, out var value) && string.Equals(value, filter.Value, StringComparison.OrdinalIgnoreCase));
    }

    private static double CalculateCosineDistanceLocal(float[] v1, float[] v2)
    {
        if (v1.Length != v2.Length || v1.Length == 0) return 1d;
        double dot = 0d, norm1 = 0d, norm2 = 0d;
        for (int i = 0; i < v1.Length; i++)
        {
            dot += v1[i] * v2[i];
            norm1 += v1[i] * v1[i];
            norm2 += v2[i] * v2[i];
        }
        if (norm1 == 0d || norm2 == 0d) return 1d;
        var similarity = dot / (Math.Sqrt(norm1) * Math.Sqrt(norm2));
        return Math.Clamp(1d - similarity, 0d, 1d);
    }

    private static double ScoreContent(string content, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return 1d;
        }

        var occurrences = content.Split(query, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length - 1;
        if (occurrences <= 0)
        {
            return 0d;
        }

        return Math.Min(1d, occurrences / 5d + 0.2d);
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
