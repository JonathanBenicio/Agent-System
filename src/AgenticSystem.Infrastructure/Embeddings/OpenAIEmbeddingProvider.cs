using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using AgenticSystem.Core.LLM.Interfaces;
using AgenticSystem.Infrastructure.Configuration;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace AgenticSystem.Infrastructure.Embeddings;

/// <summary>
/// OpenAI Embeddings provider (text-embedding-3-small/large, ada-002).
/// </summary>
public class OpenAIEmbeddingProvider : IEmbeddingProvider
{
    private readonly HttpClient _httpClient;
    private readonly OpenAISettings _settings;
    private readonly ILogger<OpenAIEmbeddingProvider> _logger;

    public OpenAIEmbeddingProvider(HttpClient httpClient, IOptions<OpenAISettings> settings, ILogger<OpenAIEmbeddingProvider> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;

        _httpClient.BaseAddress = new Uri(_settings.BaseUrl);
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_settings.ApiKey}");
    }

    public string Name => "OpenAI";
    public string DefaultModel => "text-embedding-3-small";
    public int Dimensions => 1536;
    public bool IsEnabled => _settings.Enabled && !string.IsNullOrWhiteSpace(_settings.ApiKey);
    public int Priority => _settings.Priority;

    public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken ct = default)
    {
        var results = await GenerateEmbeddingsAsync(new[] { text }, ct);
        return results[0];
    }

    public async Task<IReadOnlyList<float[]>> GenerateEmbeddingsAsync(IEnumerable<string> texts, CancellationToken ct = default)
    {
        var textList = texts.ToList();
        var payload = new
        {
            model = DefaultModel,
            input = textList
        };

        try
        {
            var response = await _httpClient.PostAsJsonAsync("v1/embeddings", payload, ct);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<EmbeddingApiResponse>(cancellationToken: ct);

            if (result?.Data is null || result.Data.Count == 0)
                throw new InvalidOperationException("No embeddings returned");

            _logger.LogDebug("📐 OpenAI Embeddings: {Count} vectors, {Tokens} tokens",
                result.Data.Count, result.Usage?.TotalTokens ?? 0);

            return result.Data
                .OrderBy(d => d.Index)
                .Select(d => d.Embedding)
                .ToList()
                .AsReadOnly();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "❌ Embedding request failed: {Message}", ex.Message);
            throw;
        }
    }

    public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        if (!IsEnabled) return false;
        try
        {
            var response = await _httpClient.GetAsync("v1/models", ct);
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
    }
}

internal class EmbeddingApiResponse
{
    [JsonPropertyName("data")]
    public List<EmbeddingData>? Data { get; set; }

    [JsonPropertyName("usage")]
    public EmbeddingUsage? Usage { get; set; }
}

internal class EmbeddingData
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("embedding")]
    public float[] Embedding { get; set; } = Array.Empty<float>();
}

internal class EmbeddingUsage
{
    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; set; }
}
