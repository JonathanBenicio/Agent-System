using System.Text.Json;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Infrastructure.Memory;

/// <summary>
/// A persistent SQLite-based Vector Store for local-first execution using Entity Framework Core.
/// Implements cosine similarity search by loading vectors into memory from the database.
/// Part of the "Local Setup & Out-of-the-Box RAG" (Phase 1).
/// </summary>
public sealed class SqliteVectorStore : IVectorStore
{
    private readonly IDbContextFactory<Persistence.AgenticDbContext> _dbContextFactory;
    private readonly ILogger<SqliteVectorStore> _logger;
    private readonly IEmbeddingGenerator<string, Embedding<float>>? _embeddingGenerator;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public SqliteVectorStore(
        IDbContextFactory<Persistence.AgenticDbContext> dbContextFactory,
        ILogger<SqliteVectorStore> logger,
        IEmbeddingGenerator<string, Embedding<float>>? embeddingGenerator = null)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
        _embeddingGenerator = embeddingGenerator;
        
        // Ensure database is created
        using var db = _dbContextFactory.CreateDbContext();
        db.Database.EnsureCreated();
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
            entity.EmbeddingData = document.Embedding != null ? VectorToBytes(document.Embedding) : null;
            entity.MetadataJson = JsonSerializer.Serialize(document.Metadata, JsonOptions);
            entity.ContextualSummary = document.ContextualSummary;
            entity.IndexedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();
        _logger.LogDebug("📦 Upserted document {Id} to SQLite via EF Core", document.Id);
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
        if (entities.Count > 0)
        {
            db.VectorDocuments.RemoveRange(entities);
            await db.SaveChangesAsync();
        }
        _logger.LogDebug("🗑️ Deleted {Count} document(s) with id {Id} from SQLite", entities.Count, id);
    }

    public async Task<SearchResult> SearchAsync(string query, SearchScope scope = SearchScope.All, int maxResults = 10)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
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
                _logger.LogWarning(ex, "Failed to generate query embedding for SQLite search.");
            }
        }

        await using var db = await _dbContextFactory.CreateDbContextAsync();
        var collection = scope != SearchScope.All ? ScopeToCollection(scope) : null;

        var dataQuery = db.VectorDocuments.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(collection))
        {
            dataQuery = dataQuery.Where(item => item.Collection == collection);
        }

        // SQLite doesn't have native vector search in EF Core yet, so we load candidates and score in-memory
        // Optimization: Filter by keywords if embedding is not available or as a pre-filter
        var candidates = await dataQuery.ToListAsync();

        var matches = candidates
            .Select(item => MapToSearchMatch(item, CalculateScore(item, query, queryEmbedding)))
            .Where(m => m.Score > 0.1)
            .OrderByDescending(m => m.Score)
            .Take(maxResults)
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

    public async Task<SearchResult> SearchWithFiltersAsync(string query, Dictionary<string, string> filters)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        
        var dataQuery = db.VectorDocuments.AsNoTracking().AsQueryable();

        if (filters.TryGetValue("type", out var typeFilter))
            dataQuery = dataQuery.Where(item => item.Type == typeFilter);
        if (filters.TryGetValue("collection", out var collectionFilter))
            dataQuery = dataQuery.Where(item => item.Collection == collectionFilter);
        if (filters.TryGetValue("id", out var idFilter))
            dataQuery = dataQuery.Where(item => item.Id == idFilter);

        var candidates = await dataQuery.ToListAsync();
        
        float[]? queryEmbedding = null;
        if (!string.IsNullOrWhiteSpace(query) && query != "*" && _embeddingGenerator != null)
        {
             var generated = await _embeddingGenerator.GenerateAsync(query);
             queryEmbedding = generated.Vector.ToArray();
        }

        var remainingFilters = filters
            .Where(item => item.Key is not ("type" or "collection" or "id"))
            .ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase);

        var filtered = candidates.Where(item => MetadataMatches(item.MetadataJson, remainingFilters));

        var matches = filtered
            .Select(item => MapToSearchMatch(item, CalculateScore(item, query, queryEmbedding)))
            .OrderByDescending(item => item.Score)
            .Take(10)
            .ToList();

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
        if (entities.Count > 0)
        {
            db.VectorDocuments.RemoveRange(entities);
            await db.SaveChangesAsync();
        }
        _logger.LogInformation("🧹 Cleaned up {Count} old documents from SQLite", entities.Count);
    }

    public async Task<VectorStoreStats> GetStatsAsync(string tenantId, CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var stats = await db.VectorDocuments
            .Where(x => x.TenantId == tenantId)
            .GroupBy(x => x.TenantId)
            .Select(g => new VectorStoreStats
            {
                TenantId = g.Key,
                DocumentCount = g.Count(),
                // Simplification for SQLite: length of text and embeddings roughly calculated
                TotalBytes = g.Sum(x => x.Content.Length * 2 + (x.EmbeddingData != null ? x.EmbeddingData.Length : 0))
            })
            .FirstOrDefaultAsync(ct);

        return stats ?? new VectorStoreStats { TenantId = tenantId, DocumentCount = 0, TotalBytes = 0 };
    }

    private double CalculateScore(VectorDocumentEntity entity, string query, float[]? queryEmbedding)
    {
        if (queryEmbedding != null && entity.EmbeddingData != null)
        {
            var vector = BytesToVector(entity.EmbeddingData);
            return 1d - CalculateCosineDistanceLocal(vector, queryEmbedding);
        }
        
        // Simple lexical fallback
        if (string.IsNullOrWhiteSpace(query) || query == "*") return 1.0;
        return entity.Content.Contains(query, StringComparison.OrdinalIgnoreCase) ? 0.8 : 0.0;
    }

    private static VectorDocumentEntity MapToEntity(EmbeddingDocument document)
    {
        return new VectorDocumentEntity
        {
            Id = document.Id,
            Content = document.Content,
            Type = document.Type,
            Collection = document.Collection,
            EmbeddingData = document.Embedding != null ? VectorToBytes(document.Embedding) : null,
            MetadataJson = JsonSerializer.Serialize(document.Metadata, JsonOptions),
            ContextualSummary = document.ContextualSummary,
            IndexedAt = DateTime.UtcNow
        };
    }

    private static SearchMatch MapToSearchMatch(VectorDocumentEntity entity, double score)
    {
        return new SearchMatch
        {
            Id = entity.Id,
            Content = entity.Content,
            Type = entity.Type,
            Collection = entity.Collection,
            Score = score,
            Metadata = JsonSerializer.Deserialize<Dictionary<string, string>>(entity.MetadataJson, JsonOptions) ?? new(),
            Snippet = entity.Content.Length > 300 ? entity.Content[..300] + "..." : entity.Content,
            Embedding = entity.EmbeddingData != null ? BytesToVector(entity.EmbeddingData) : null,
            ContextualSummary = entity.ContextualSummary,
            IndexedAt = entity.IndexedAt
        };
    }

    private static byte[] VectorToBytes(float[] vector)
    {
        var bytes = new byte[vector.Length * sizeof(float)];
        Buffer.BlockCopy(vector, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    private static float[] BytesToVector(byte[] bytes)
    {
        var vector = new float[bytes.Length / sizeof(float)];
        Buffer.BlockCopy(bytes, 0, vector, 0, bytes.Length);
        return vector;
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

    private static bool MetadataMatches(string metadataJson, IReadOnlyDictionary<string, string> filters)
    {
        if (filters.Count == 0) return true;
        var metadata = JsonSerializer.Deserialize<Dictionary<string, string>>(metadataJson, JsonOptions) ?? new();
        return filters.All(f => metadata.TryGetValue(f.Key, out var v) && string.Equals(v, f.Value, StringComparison.OrdinalIgnoreCase));
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
