using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Services;

/// <summary>
/// Gerador de embeddings multi-provider (OpenAI, Google, Ollama).
/// </summary>
public class HttpEmbeddingGenerator : IEmbeddingGenerator
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<HttpEmbeddingGenerator> _logger;

    public HttpEmbeddingGenerator(IHttpClientFactory httpClientFactory, ILogger<HttpEmbeddingGenerator> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<float[]> GenerateAsync(string text, EmbeddingModelConfig model)
    {
        var results = await GenerateBatchAsync(new[] { text }, model);
        return results.First();
    }

    public async Task<IEnumerable<float[]>> GenerateBatchAsync(IEnumerable<string> texts, EmbeddingModelConfig model)
    {
        var textList = texts.ToList();
        var client = _httpClientFactory.CreateClient();

        return model.Provider switch
        {
            EmbeddingProvider.OpenAI => await GenerateOpenAIAsync(client, textList, model),
            EmbeddingProvider.Google => await GenerateGoogleAsync(client, textList, model),
            EmbeddingProvider.Ollama => await GenerateOllamaAsync(client, textList, model),
            _ => throw new NotSupportedException($"Provider '{model.Provider}' not supported.")
        };
    }

    private async Task<IEnumerable<float[]>> GenerateOpenAIAsync(
        HttpClient client, List<string> texts, EmbeddingModelConfig model)
    {
        var baseUrl = model.BaseUrl ?? "https://api.openai.com";
        var url = $"{baseUrl.TrimEnd('/')}/v1/embeddings";

        client.DefaultRequestHeaders.Clear();
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {model.ApiKey}");

        var request = new { input = texts, model = model.ModelName };
        var response = await client.PostAsJsonAsync(url, request);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var data = json.GetProperty("data");

        return data.EnumerateArray()
            .OrderBy(d => d.GetProperty("index").GetInt32())
            .Select(d => d.GetProperty("embedding").EnumerateArray().Select(v => v.GetSingle()).ToArray())
            .ToList();
    }

    private async Task<IEnumerable<float[]>> GenerateGoogleAsync(
        HttpClient client, List<string> texts, EmbeddingModelConfig model)
    {
        var baseUrl = model.BaseUrl ?? "https://generativelanguage.googleapis.com";
        var url = $"{baseUrl.TrimEnd('/')}/v1beta/models/{model.ModelName}:batchEmbedContents?key={model.ApiKey}";

        var requests = texts.Select(t => new { model = $"models/{model.ModelName}", content = new { parts = new[] { new { text = t } } } });
        var body = new { requests };

        var response = await client.PostAsJsonAsync(url, body);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var embeddings = json.GetProperty("embeddings");

        return embeddings.EnumerateArray()
            .Select(e => e.GetProperty("values").EnumerateArray().Select(v => v.GetSingle()).ToArray())
            .ToList();
    }

    private async Task<IEnumerable<float[]>> GenerateOllamaAsync(
        HttpClient client, List<string> texts, EmbeddingModelConfig model)
    {
        var baseUrl = model.BaseUrl ?? "http://localhost:11434";
        var results = new List<float[]>();

        foreach (var text in texts)
        {
            var url = $"{baseUrl.TrimEnd('/')}/api/embeddings";
            var request = new { model = model.ModelName, prompt = text };
            var response = await client.PostAsJsonAsync(url, request);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            var embedding = json.GetProperty("embedding").EnumerateArray()
                .Select(v => v.GetSingle()).ToArray();
            results.Add(embedding);
        }

        return results;
    }
}
