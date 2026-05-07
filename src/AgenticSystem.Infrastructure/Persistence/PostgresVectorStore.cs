using System.Text.Json;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Infrastructure.Persistence;

/// <summary>
/// Implementação PostgreSQL de IVectorStore com busca textual.
/// Utiliza tabela vector_documents com coluna metadata JSONB.
/// Para busca vetorial real com pgvector, substituir CalculateRelevance por cosine similarity SQL.
/// </summary>
public class PostgresVectorStore : IVectorStore
{
    private readonly IDbContextFactory<AgenticDbContext> _dbContextFactory;
    private readonly ILogger<PostgresVectorStore> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public PostgresVectorStore(IDbContextFactory<AgenticDbContext> dbContextFactory, ILogger<PostgresVectorStore> logger)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
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
            entity.Embedding = document.Embedding;
            entity.MetadataJson = JsonSerializer.Serialize(document.Metadata, JsonOptions);
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

        var candidatesQuery = db.VectorDocuments.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(collection))
        {
            candidatesQuery = candidatesQuery.Where(item => item.Collection == collection);
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            candidatesQuery = candidatesQuery.Where(item => EF.Functions.ILike(item.Content, $"%{query}%"));
        }

        var candidates = await candidatesQuery
            .OrderByDescending(item => item.IndexedAt)
            .Take(Math.Max(maxResults * 5, maxResults))
            .ToListAsync();

        var matches = candidates
            .Select(item => MapToSearchMatch(item, ScoreContent(item.Content, query)))
            .OrderByDescending(item => item.Score)
            .Take(maxResults)
            .ToList();

        sw.Stop();

        return new SearchResult
        {
            Query = query,
            Scope = scope,
            Matches = matches.Take(maxResults).ToList(),
            TotalFound = matches.Count,
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

        if (hasQuery)
        {
            dataQuery = dataQuery.Where(item => EF.Functions.ILike(item.Content, $"%{normalizedQuery}%"));
        }

        var candidates = await dataQuery
            .OrderByDescending(item => item.IndexedAt)
            .Take(hasQuery ? 50 : 200)
            .ToListAsync();

        var remainingFilters = filters
            .Where(item => item.Key is not ("type" or "collection" or "id"))
            .ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase);

        var filtered = candidates.Where(item => MetadataMatches(item.MetadataJson, remainingFilters));
        var matches = filtered
            .Select(item => MapToSearchMatch(item, hasQuery ? ScoreContent(item.Content, normalizedQuery) : 1d))
            .OrderByDescending(item => item.Score)
            .ThenByDescending(item => item.IndexedAt)
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
        var deleted = entities.Count;
        if (deleted > 0)
        {
            db.VectorDocuments.RemoveRange(entities);
            await db.SaveChangesAsync();
        }

        _logger.LogInformation("Cleaned up {Count} old documents from PostgreSQL (older than {Days} days)",
            deleted, olderThan.TotalDays);
    }

    private static EmbeddingDocument MapToModel(VectorDocumentEntity entity)
    {
        return new EmbeddingDocument
        {
            Id = entity.Id,
            Content = entity.Content,
            Type = entity.Type,
            Collection = entity.Collection,
            Embedding = entity.Embedding,
            Metadata = JsonSerializer.Deserialize<Dictionary<string, string>>(entity.MetadataJson, JsonOptions) ?? new(),
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
            Embedding = document.Embedding,
            MetadataJson = JsonSerializer.Serialize(document.Metadata, JsonOptions),
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
