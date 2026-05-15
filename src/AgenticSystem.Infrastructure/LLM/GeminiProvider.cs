using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using AgenticSystem.Core.LLM.Interfaces;
using AgenticSystem.Core.LLM.Models;
using AgenticSystem.Infrastructure.Configuration;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace AgenticSystem.Infrastructure.LLM;

/// <summary>
/// Google Gemini — provider via API REST Generative Language.
/// </summary>
public class GeminiProvider : ILLMProvider
{
    private readonly HttpClient _httpClient;
    private readonly GeminiSettings _settings;
    private readonly ILogger<GeminiProvider> _logger;

    public GeminiProvider(HttpClient httpClient, IOptions<GeminiSettings> settings, ILogger<GeminiProvider> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;

        _httpClient.BaseAddress = new Uri(_settings.BaseUrl);
        UpdateInternalHeaders();
    }

    private void UpdateInternalHeaders()
    {
        _httpClient.DefaultRequestHeaders.Remove("X-Agentic-ProviderName");
        _httpClient.DefaultRequestHeaders.Remove("X-Agentic-ApiKeyId");
        
        _httpClient.DefaultRequestHeaders.Add("X-Agentic-ProviderName", Name);
        _httpClient.DefaultRequestHeaders.Add("X-Agentic-ApiKeyId", "infrastructure-global");
    }

    public string Name => "Gemini";
    public string DefaultModel => _settings.DefaultModel;
    public bool IsEnabled => _settings.Enabled && !string.IsNullOrWhiteSpace(_settings.ApiKey);
    public int Priority => _settings.Priority;

    public void Configure(string? apiKey, string? defaultModel, bool? enabled, int? priority)
    {
        if (apiKey is not null)
        {
            _settings.ApiKey = apiKey;
            UpdateInternalHeaders();
        }
        if (defaultModel is not null) _settings.DefaultModel = defaultModel;
        if (enabled.HasValue) _settings.Enabled = enabled.Value;
        if (priority.HasValue) _settings.Priority = priority.Value;
    }

    public async Task<LLMResponse> GenerateAsync(LLMRequest request, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var model = request.Model ?? DefaultModel;

        try
        {
            var contents = BuildContents(request);
            var payload = new
            {
                contents,
                generationConfig = new
                {
                    temperature = request.Parameters.Temperature,
                    maxOutputTokens = request.Parameters.MaxTokens,
                    topP = request.Parameters.TopP
                }
            };

            var url = $"v1beta/models/{model}:generateContent?key={_settings.ApiKey}";
            var response = await _httpClient.PostAsJsonAsync(url, payload, ct);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<GeminiResponse>(cancellationToken: ct);
            sw.Stop();

            var text = result?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;
            if (string.IsNullOrWhiteSpace(text))
                return LLMResponse.Fail("No content from Gemini", Name);

            var usage = new UsageInfo
            {
                PromptTokens = result?.UsageMetadata?.PromptTokenCount ?? 0,
                CompletionTokens = result?.UsageMetadata?.CandidatesTokenCount ?? 0
            };

            _logger.LogDebug("💎 Gemini [{Model}] {Tokens} tokens in {Latency}ms",
                model, usage.TotalTokens, sw.ElapsedMilliseconds);

            return new LLMResponse
            {
                Content = text,
                Model = model,
                Provider = Name,
                Usage = usage,
                Success = true,
                Latency = sw.Elapsed
            };
        }
        catch (HttpRequestException ex)
        {
            sw.Stop();
            _logger.LogError(ex, "❌ Gemini request failed: {Message}", ex.Message);
            return LLMResponse.Fail($"Gemini API error: {ex.Message}", Name);
        }
        catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
        {
            sw.Stop();
            _logger.LogError(ex, "❌ Gemini request timed out.");
            return LLMResponse.Fail("Gemini request timed out.", Name);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "❌ Unexpected error in GeminiProvider: {Message}", ex.Message);
            return LLMResponse.Fail($"Unexpected error: {ex.Message}", Name);
        }
    }

    public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_settings.ApiKey)) return false;

        try
        {
            var url = $"v1beta/models?key={_settings.ApiKey}";
            var response = await _httpClient.GetAsync(url, ct);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private static List<object> BuildContents(LLMRequest request)
    {
        var contents = new List<object>();

        if (request.Messages.Count > 0)
        {
            foreach (var msg in request.Messages)
            {
                var role = msg.Role == "assistant" ? "model" : msg.Role;
                if (role == "system") role = "user"; // Gemini trata system como user
                contents.Add(new { role, parts = new[] { new { text = msg.Content } } });
            }
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
                contents.Add(new { role = "user", parts = new[] { new { text = request.SystemPrompt } } });

            if (!string.IsNullOrWhiteSpace(request.Prompt))
                contents.Add(new { role = "user", parts = new[] { new { text = request.Prompt } } });
        }

        return contents;
    }
}

// Gemini API response models
internal class GeminiResponse
{
    [JsonPropertyName("candidates")]
    public List<GeminiCandidate>? Candidates { get; set; }

    [JsonPropertyName("usageMetadata")]
    public GeminiUsageMetadata? UsageMetadata { get; set; }
}

internal class GeminiCandidate
{
    [JsonPropertyName("content")]
    public GeminiContent? Content { get; set; }
}

internal class GeminiContent
{
    [JsonPropertyName("parts")]
    public List<GeminiPart>? Parts { get; set; }
}

internal class GeminiPart
{
    [JsonPropertyName("text")]
    public string? Text { get; set; }
}

internal class GeminiUsageMetadata
{
    [JsonPropertyName("promptTokenCount")]
    public int PromptTokenCount { get; set; }

    [JsonPropertyName("candidatesTokenCount")]
    public int CandidatesTokenCount { get; set; }
}
