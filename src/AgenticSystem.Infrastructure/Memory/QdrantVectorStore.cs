using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;

namespace AgenticSystem.Infrastructure.Memory;

/// <summary>
/// Implementação do IVectorStore para Qdrant (Distributed Vector Database).
/// </summary>
public class QdrantVectorStore : IVectorStore
{
    private readonly HttpClient _httpClient;
    private readonly MemorySettings _settings;
    private readonly ILogger<QdrantVectorStore> _logger;

    public QdrantVectorStore(
        HttpClient httpClient,
        IOptions<MemorySettings> options,
        ILogger<QdrantVectorStore> logger)
    {
        _httpClient = httpClient;
        _settings = options.Value;
        _logger = logger;

        if (!string.IsNullOrWhiteSpace(_settings.Qdrant.ApiKey))
        {
            _httpClient.DefaultRequestHeaders.Add("api-key", _settings.Qdrant.ApiKey);
        }
    }

    public async Task UpsertAsync(EmbeddingDocument document)
    {
        var collectionName = document.Collection ?? "default";
        
        // Em uma implementação real, verificaríamos se a coleção existe e a criaríamos se necessário.
        // Aqui estamos simplificando para fins de roadmap.
        
        var point = new
        {
            points = new[]
            {
                new
                {
                    id = Guid.NewGuid().ToString(), // Qdrant prefere UUIDs ou inteiros
                    vector = document.Embedding,
                    payload = new
                    {
                        content = document.Content,
                        type = document.Type,
                        metadata = System.Text.Json.JsonSerializer.Serialize(document.Metadata),
                        tenantId = "default" // TODO: Add TenantId to EmbeddingDocument in Core
                    }
                }
            }
        };

        var response = await _httpClient.PutAsJsonAsync($"{_settings.Qdrant.Url}/collections/{collectionName}/points", point);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteAsync(string id, string? collection = null)
    {
        var collectionName = collection ?? "default";
        var body = new { ids = new[] { id } };
        await _httpClient.PostAsJsonAsync($"{_settings.Qdrant.Url}/collections/{collectionName}/points/delete", body);
    }

    public async Task<SearchResult> SearchAsync(string query, SearchScope scope = SearchScope.All, int maxResults = 10)
    {
        // Nota: O SearchAsync recebe a query em texto, mas Qdrant precisa do vetor.
        // O orquestrador deve garantir que o embedding seja gerado antes ou usar um middleware.
        // Como o IVectorStore.SearchAsync não recebe o vetor diretamente, assume-se que 
        // a implementação deve lidar com isso ou o orquestrador deve passar o vetor no SearchWithFiltersAsync.
        
        _logger.LogWarning("SearchAsync called on Qdrant without vector. Returning empty result.");
        return new SearchResult { Query = query, Matches = new List<SearchMatch>() };
    }

    public async Task<SearchResult> SearchWithFiltersAsync(string query, Dictionary<string, string> filters)
    {
        // Implementação real usaria o motor de busca do Qdrant com filtros de payload.
        return new SearchResult { Query = query, Matches = new List<SearchMatch>() };
    }

    public async Task<IEnumerable<string>> GetCollectionsAsync()
    {
        var response = await _httpClient.GetFromJsonAsync<QdrantCollectionsResponse>($"{_settings.Qdrant.Url}/collections");
        return response?.Result?.Collections?.Select(c => c.Name) ?? Enumerable.Empty<string>();
    }

    public Task CleanupOldDocumentsAsync(TimeSpan olderThan)
    {
        // Qdrant não tem TTL nativo simples em todas as versões, 
        // mas podemos deletar via filtro de data se o payload tiver timestamp.
        return Task.CompletedTask;
    }

    public Task<VectorStoreStats> GetStatsAsync(string tenantId, CancellationToken ct = default)
    {
        // Not fully implemented for remote Qdrant yet. We return 0.
        return Task.FromResult(new VectorStoreStats { TenantId = tenantId, DocumentCount = 0, TotalBytes = 0 });
    }

    private class QdrantCollectionsResponse
    {
        public QdrantCollectionsResult? Result { get; set; }
    }

    private class QdrantCollectionsResult
    {
        public List<QdrantCollection>? Collections { get; set; }
    }

    private class QdrantCollection
    {
        public string Name { get; set; } = string.Empty;
    }
}
