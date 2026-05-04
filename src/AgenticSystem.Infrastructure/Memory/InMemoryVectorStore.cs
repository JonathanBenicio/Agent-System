using Microsoft.Extensions.Logging;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using System.Collections.Concurrent;

namespace AgenticSystem.Infrastructure.Memory;

public class InMemoryVectorStore : IVectorStore
{
    private readonly ConcurrentDictionary<string, List<EmbeddingDocument>> _collections = new();
    private readonly ILogger<InMemoryVectorStore> _logger;

    public InMemoryVectorStore(ILogger<InMemoryVectorStore> logger)
    {
        _logger = logger;
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

    public Task<SearchResult> SearchAsync(string query, SearchScope scope = SearchScope.All, int maxResults = 10)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var queryLower = query.ToLowerInvariant();
        var matches = new List<SearchMatch>();

        var targetCollections = scope == SearchScope.All
            ? _collections
            : _collections.Where(c => MatchesScope(c.Key, scope));

        foreach (var (collectionName, docs) in targetCollections)
        {
            lock (docs)
            {
                foreach (var doc in docs)
                {
                    var score = CalculateRelevance(queryLower, doc);
                    if (score > 0.1)
                    {
                        matches.Add(new SearchMatch
                        {
                            Id = doc.Id,
                            Content = doc.Content,
                            Type = doc.Type,
                            Score = score,
                            Metadata = doc.Metadata,
                            Snippet = ExtractSnippet(doc.Content, queryLower)
                        });
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

        _logger.LogDebug("🔍 Search '{Query}': {Found} matches in {Time}ms", query, result.TotalFound, sw.ElapsedMilliseconds);
        return Task.FromResult(result);
    }

    public Task<SearchResult> SearchWithFiltersAsync(string query, Dictionary<string, string> filters)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var queryLower = query.ToLowerInvariant();
        var matches = new List<SearchMatch>();

        foreach (var (_, docs) in _collections)
        {
            lock (docs)
            {
                foreach (var doc in docs)
                {
                    if (!MatchesFilters(doc, filters)) continue;

                    var score = CalculateRelevance(queryLower, doc);
                    if (score > 0.1)
                    {
                        matches.Add(new SearchMatch
                        {
                            Id = doc.Id,
                            Content = doc.Content,
                            Type = doc.Type,
                            Score = score,
                            Metadata = doc.Metadata,
                            Snippet = ExtractSnippet(doc.Content, queryLower)
                        });
                    }
                }
            }
        }

        sw.Stop();

        return Task.FromResult(new SearchResult
        {
            Query = query,
            Matches = matches.OrderByDescending(m => m.Score).Take(10).ToList(),
            TotalFound = matches.Count,
            ExecutionTime = sw.Elapsed
        });
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

    private static double CalculateRelevance(string query, EmbeddingDocument doc)
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
            if (doc.Metadata.TryGetValue(key, out var metaValue) &&
                !metaValue.Equals(value, StringComparison.OrdinalIgnoreCase))
                return false;
        }
        return true;
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
