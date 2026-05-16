using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Text.Json;

namespace AgenticSystem.Infrastructure.Memory;

/// <summary>
/// IVectorStore implementation for Pinecone (Serverless Vector Database).
/// Uses the Pinecone REST API for upsert, delete, query, and list operations.
/// Part of "Enterprise Scale" (Phase 4).
/// </summary>
public sealed class PineconeVectorStore : IVectorStore
{
    private readonly HttpClient _httpClient;
    private readonly PineconeSettings _settings;
    private readonly ILogger<PineconeVectorStore> _logger;

    public PineconeVectorStore(
        HttpClient httpClient,
        IOptions<PineconeSettings> options,
        ILogger<PineconeVectorStore> logger)
    {
        _httpClient = httpClient;
        _settings = options.Value;
        _logger = logger;

        _httpClient.BaseAddress = new Uri(_settings.Host.TrimEnd('/') + "/");
        _httpClient.DefaultRequestHeaders.Add("Api-Key", _settings.ApiKey);
    }

    public async Task UpsertAsync(EmbeddingDocument document)
    {
        var ns = document.Collection ?? _settings.Namespace;
        var body = new
        {
            vectors = new[]
            {
                new
                {
                    id = document.Id ?? Guid.NewGuid().ToString("N"),
                    values = document.Embedding,
                    metadata = new Dictionary<string, object>
                    {
                        ["content"] = document.Content,
                        ["type"] = document.Type,
                        ["metadata"] = JsonSerializer.Serialize(document.Metadata)
                    }
                }
            },
            @namespace = ns
        };

        var response = await _httpClient.PostAsJsonAsync("vectors/upsert", body);
        response.EnsureSuccessStatusCode();
        _logger.LogDebug("📌 Pinecone upsert complete (namespace: {Namespace})", ns);
    }

    public async Task DeleteAsync(string id, string? collection = null)
    {
        var ns = collection ?? _settings.Namespace;
        var body = new
        {
            ids = new[] { id },
            @namespace = ns
        };

        var response = await _httpClient.PostAsJsonAsync("vectors/delete", body);
        response.EnsureSuccessStatusCode();
    }

    public async Task<SearchResult> SearchAsync(string query, SearchScope scope = SearchScope.All, int maxResults = 10)
    {
        // Pinecone requires vector input for queries, not raw text.
        // The orchestrator should provide embeddings via SearchWithFiltersAsync or 
        // a middleware that converts text → embedding before calling this store.
        _logger.LogWarning("SearchAsync called on Pinecone without vector. Use SearchWithFiltersAsync with pre-computed embeddings.");
        return new SearchResult { Query = query, Matches = new List<SearchMatch>() };
    }

    public async Task<SearchResult> SearchWithFiltersAsync(string query, Dictionary<string, string> filters)
    {
        var ns = filters.GetValueOrDefault("namespace") ?? _settings.Namespace;
        var topK = int.TryParse(filters.GetValueOrDefault("topK"), out var k) ? k : 10;

        // Build Pinecone filter from provided filters (excluding internal keys)
        var pineconeFilter = filters
            .Where(f => f.Key is not "namespace" and not "topK" and not "vector")
            .ToDictionary(
                f => f.Key,
                f => (object)new Dictionary<string, string> { ["$eq"] = f.Value });

        var body = new
        {
            @namespace = ns,
            topK,
            includeMetadata = true,
            filter = pineconeFilter.Count > 0 ? pineconeFilter : null
        };

        var response = await _httpClient.PostAsJsonAsync("query", body);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<PineconeQueryResponse>();
        var matches = result?.Matches?.Select(m => new SearchMatch
        {
            Content = m.Metadata?.GetValueOrDefault("content")?.ToString() ?? string.Empty,
            Score = (double)m.Score,
            Collection = $"pinecone:{ns}",
            Metadata = m.Metadata?.ToDictionary(kv => kv.Key, kv => kv.Value?.ToString() ?? string.Empty)
                       ?? new Dictionary<string, string>()
        }).ToList() ?? new List<SearchMatch>();

        return new SearchResult { Query = query, Matches = matches };
    }

    public async Task<IEnumerable<string>> GetCollectionsAsync()
    {
        // Pinecone uses "namespaces" within an index, not separate collections.
        try
        {
            var response = await _httpClient.GetFromJsonAsync<PineconeDescribeResponse>("describe_index_stats");
            return response?.Namespaces?.Keys ?? Enumerable.Empty<string>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to list Pinecone namespaces");
            return Enumerable.Empty<string>();
        }
    }

    public Task CleanupOldDocumentsAsync(TimeSpan olderThan)
    {
        // Pinecone doesn't support TTL-based cleanup natively.
        // Would need metadata-based filtering + batch delete.
        _logger.LogDebug("CleanupOldDocumentsAsync is not natively supported by Pinecone.");
        return Task.CompletedTask;
    }

    public Task<VectorStoreStats> GetStatsAsync(string tenantId, CancellationToken ct = default)
    {
        // Basic stats — full implementation would call describe_index_stats with namespace filter
        return Task.FromResult(new VectorStoreStats
        {
            TenantId = tenantId,
            DocumentCount = 0,
            TotalBytes = 0
        });
    }

    #region Pinecone API DTOs

    private sealed class PineconeQueryResponse
    {
        public List<PineconeMatch>? Matches { get; set; }
    }

    private sealed class PineconeMatch
    {
        public string Id { get; set; } = string.Empty;
        public float Score { get; set; }
        public Dictionary<string, object>? Metadata { get; set; }
    }

    private sealed class PineconeDescribeResponse
    {
        public Dictionary<string, object>? Namespaces { get; set; }
    }

    #endregion
}
