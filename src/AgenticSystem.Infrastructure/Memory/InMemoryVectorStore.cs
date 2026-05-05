using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using System.Collections.Concurrent;

namespace AgenticSystem.Infrastructure.Memory;

using TextEmbeddingGenerator = Microsoft.Extensions.AI.IEmbeddingGenerator<string, Microsoft.Extensions.AI.Embedding<float>>;

public class InMemoryVectorStore : IVectorStore
{
    private readonly ConcurrentDictionary<string, List<EmbeddingDocument>> _collections = new();
    private readonly TextEmbeddingGenerator? _embeddingGenerator;
    private readonly ILogger<InMemoryVectorStore> _logger;

    public InMemoryVectorStore(ILogger<InMemoryVectorStore> logger, TextEmbeddingGenerator? embeddingGenerator = null)
    {
        _logger = logger;
        _embeddingGenerator = embeddingGenerator;
    }

    public Task UpsertAsync(EmbeddingDocument document)
    {
        var collection = _collections.GetOrAdd(document.Collection, _ => new List<EmbeddingDocument>());

        lock (collection)
        {
            collection.RemoveAll(d => d.Id == document.Id);
            document.IndexedAt = DateTime.UtcNow;
            collection.Add(document);
        }

        _logger.LogDebug("📦 Upserted document {Id} in collection {Collection}", document.Id, document.Collection);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string id, string? collection = null)
    {
        var removed = 0;

        if (!string.IsNullOrWhiteSpace(collection))
        {
            if (_collections.TryGetValue(collection, out var docs))
            {
                lock (docs)
                {
                    removed = docs.RemoveAll(d => d.Id == id);
                }
            }
        }
        else
        {
            foreach (var (_, docs) in _collections)
            {
                lock (docs)
                {
                    removed += docs.RemoveAll(d => d.Id == id);
                }
            }
        }

        _logger.LogDebug("🗑️ Deleted {Count} document(s) with id {Id} from collection {Collection}",
            removed, id, collection ?? "*");

        return Task.CompletedTask;
    }

    public async Task<SearchResult> SearchAsync(string query, SearchScope scope = SearchScope.All, int maxResults = 10)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var queryLower = query.ToLowerInvariant();
        var matches = new List<SearchMatch>();

        float[]? queryEmbedding = await GenerateQueryEmbeddingAsync(query);

        var targetCollections = scope == SearchScope.All
            ? _collections
            : _collections.Where(c => MatchesScope(c.Key, scope));

        foreach (var (collectionName, docs) in targetCollections)
        {
            lock (docs)
            {
                foreach (var doc in docs)
                {
                    var score = CalculateRelevance(queryLower, doc, queryEmbedding);
                    if (score > 0.1)
                    {
                        matches.Add(CreateSearchMatch(doc, score, ExtractSnippet(doc.Content, queryLower)));
                    }
                }
            }
        }

        sw.Stop();

        var result = new SearchResult
        {
            Query = query,
            Scope = scope,
            Matches = matches.OrderByDescending(m => m.Score).Take(maxResults).ToList(),
            TotalFound = matches.Count,
            ExecutionTime = sw.Elapsed
        };

        _logger.LogDebug("🔍 Search '{Query}': {Found} matches in {Time}ms (semantic: {Semantic})",
            query, result.TotalFound, sw.ElapsedMilliseconds, queryEmbedding != null);
        return result;
    }

    public async Task<SearchResult> SearchWithFiltersAsync(string query, Dictionary<string, string> filters)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var normalizedQuery = query.Trim();
        var hasQuery = !string.IsNullOrWhiteSpace(normalizedQuery) && normalizedQuery != "*";
        var queryLower = normalizedQuery.ToLowerInvariant();
        var matches = new List<SearchMatch>();

        float[]? queryEmbedding = hasQuery ? await GenerateQueryEmbeddingAsync(normalizedQuery) : null;

        foreach (var (_, docs) in _collections)
        {
            lock (docs)
            {
                foreach (var doc in docs)
                {
                    if (!MatchesFilters(doc, filters)) continue;

                    var score = hasQuery ? CalculateRelevance(queryLower, doc, queryEmbedding) : 1.0;
                    if (!hasQuery || score > 0.1)
                    {
                        matches.Add(CreateSearchMatch(
                            doc,
                            score,
                            hasQuery ? ExtractSnippet(doc.Content, queryLower) : null));
                    }
                }
            }
        }

        sw.Stop();

        return new SearchResult
        {
            Query = query,
            Matches = hasQuery
                ? matches.OrderByDescending(m => m.Score).Take(10).ToList()
                : matches,
            TotalFound = matches.Count,
            ExecutionTime = sw.Elapsed
        };
    }

    public Task<IEnumerable<string>> GetCollectionsAsync()
    {
        return Task.FromResult(_collections.Keys.AsEnumerable());
    }

    public Task CleanupOldDocumentsAsync(TimeSpan olderThan)
    {
        var cutoff = DateTime.UtcNow - olderThan;
        var totalRemoved = 0;

        foreach (var (name, docs) in _collections)
        {
            lock (docs)
            {
                var removed = docs.RemoveAll(d => d.IndexedAt < cutoff);
                totalRemoved += removed;
            }
        }

        _logger.LogInformation("🧹 Cleaned up {Count} old documents (older than {Days} days)",
            totalRemoved, olderThan.TotalDays);
        return Task.CompletedTask;
    }

    private static double CalculateRelevance(string query, EmbeddingDocument doc, float[]? queryEmbedding)
    {
        // Semantic search via cosine similarity when embeddings are available
        if (queryEmbedding != null && doc.Embedding is { Length: > 0 })
        {
            var cosine = CosineSimilarity(queryEmbedding, doc.Embedding);
            // Normalize from [-1,1] to [0,1]
            return (cosine + 1.0) / 2.0;
        }

        // Fallback: lexical search
        return CalculateLexicalRelevance(query, doc);
    }

    private static double CalculateLexicalRelevance(string query, EmbeddingDocument doc)
    {
        var contentLower = doc.Content.ToLowerInvariant();
        var words = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (words.Length == 0) return 0;

        var matchCount = words.Count(w => contentLower.Contains(w));
        var score = (double)matchCount / words.Length;

        // Boost for exact phrase match
        if (contentLower.Contains(query)) score = Math.Min(score + 0.3, 1.0);

        // Boost for title/metadata match
        if (doc.Metadata.TryGetValue("title", out var title) &&
            title.ToLowerInvariant().Contains(query))
            score = Math.Min(score + 0.2, 1.0);

        return score;
    }

    private static double CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length) return 0;

        double dot = 0, normA = 0, normB = 0;
        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * (double)b[i];
            normA += a[i] * (double)a[i];
            normB += b[i] * (double)b[i];
        }

        var denominator = Math.Sqrt(normA) * Math.Sqrt(normB);
        return denominator == 0 ? 0 : dot / denominator;
    }

    private async Task<float[]?> GenerateQueryEmbeddingAsync(string query)
    {
        if (_embeddingGenerator is null)
            return null;

        try
        {
            var embedding = await _embeddingGenerator.GenerateAsync(query);
            return embedding.Vector.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate query embedding, falling back to lexical search");
            return null;
        }
    }

    private static bool MatchesScope(string collectionName, SearchScope scope)
    {
        return scope switch
        {
            SearchScope.Notes => collectionName == "notes",
            SearchScope.Agents => collectionName == "agents",
            SearchScope.Decisions => collectionName == "decisions",
            SearchScope.Domain => collectionName == "domain",
            _ => true
        };
    }

    private static bool MatchesFilters(EmbeddingDocument doc, Dictionary<string, string> filters)
    {
        foreach (var (key, value) in filters)
        {
            if (key == "type" && !doc.Type.Equals(value, StringComparison.OrdinalIgnoreCase))
                return false;
            if (key == "collection" && !doc.Collection.Equals(value, StringComparison.OrdinalIgnoreCase))
                return false;
            if (key == "id" && !doc.Id.Equals(value, StringComparison.OrdinalIgnoreCase))
                return false;
            if (key is "type" or "collection" or "id")
                continue;
            if (!doc.Metadata.TryGetValue(key, out var metaValue) ||
                !metaValue.Equals(value, StringComparison.OrdinalIgnoreCase))
                return false;
        }
        return true;
    }

    private static SearchMatch CreateSearchMatch(EmbeddingDocument doc, double score, string? snippet)
    {
        return new SearchMatch
        {
            Id = doc.Id,
            Content = doc.Content,
            Type = doc.Type,
            Collection = doc.Collection,
            Score = score,
            Metadata = doc.Metadata,
            Snippet = snippet,
            Embedding = doc.Embedding,
            IndexedAt = doc.IndexedAt
        };
    }

    private static string ExtractSnippet(string content, string query, int contextChars = 150)
    {
        var idx = content.IndexOf(query, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return content.Length > contextChars ? content[..contextChars] + "..." : content;

        var start = Math.Max(0, idx - contextChars / 2);
        var end = Math.Min(content.Length, idx + query.Length + contextChars / 2);
        var snippet = content[start..end];

        if (start > 0) snippet = "..." + snippet;
        if (end < content.Length) snippet += "...";

        return snippet;
    }
}
