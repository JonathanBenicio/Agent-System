using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using AgenticSystem.Core.LLM.Interfaces;
using AgenticSystem.Core.LLM.Models;
using AgenticSystem.Infrastructure.Configuration;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgenticSystem.Infrastructure.LLM;

public class OpenRouterProvider : ILLMProvider
{
    private readonly HttpClient _httpClient;
    private readonly OpenRouterSettings _settings;
    private readonly ILogger<OpenRouterProvider> _logger;

    public OpenRouterProvider(HttpClient httpClient, IOptions<OpenRouterSettings> settings, ILogger<OpenRouterProvider> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;

        _httpClient.BaseAddress = new Uri(_settings.BaseUrl);
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_settings.ApiKey}");
        _httpClient.DefaultRequestHeaders.Add("HTTP-Referer", "https://github.com/JonathanBenicio/Agent-System");
        _httpClient.DefaultRequestHeaders.Add("X-Title", "AgenticSystem");
        UpdateInternalHeaders();
    }

    private void UpdateInternalHeaders()
    {
        _httpClient.DefaultRequestHeaders.Remove("X-Agentic-ProviderName");
        _httpClient.DefaultRequestHeaders.Remove("X-Agentic-ApiKeyId");
        
        _httpClient.DefaultRequestHeaders.Add("X-Agentic-ProviderName", Name);
        _httpClient.DefaultRequestHeaders.Add("X-Agentic-ApiKeyId", "infrastructure-global");
    }

    public string Name => "OpenRouter";
    public string DefaultModel => _settings.DefaultModel;
    public bool IsEnabled => _settings.Enabled && !string.IsNullOrWhiteSpace(_settings.ApiKey);
    public int Priority => _settings.Priority;

    public void Configure(string? apiKey, string? defaultModel, bool? enabled, int? priority)
    {
        if (apiKey is not null)
        {
            _settings.ApiKey = apiKey;
            _httpClient.DefaultRequestHeaders.Remove("Authorization");
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_settings.ApiKey}");
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
            var messages = BuildMessages(request);
            var payload = new
            {
                model,
                messages,
                temperature = request.Parameters.Temperature,
                max_tokens = request.Parameters.MaxTokens,
                top_p = request.Parameters.TopP,
                frequency_penalty = request.Parameters.FrequencyPenalty,
                presence_penalty = request.Parameters.PresencePenalty,
                stop = request.Parameters.Stop
            };

            var response = await _httpClient.PostAsJsonAsync("v1/chat/completions", payload, ct);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<OpenRouterChatResponse>(cancellationToken: ct);
            sw.Stop();

            if (result?.Choices is null || result.Choices.Count == 0)
                return LLMResponse.Fail("No choices returned", Name);

            var content = result.Choices[0].Message?.Content ?? string.Empty;
            var usage = new UsageInfo
            {
                PromptTokens = result.Usage?.PromptTokens ?? 0,
                CompletionTokens = result.Usage?.CompletionTokens ?? 0
            };

            _logger.LogDebug("🤖 OpenRouter [{Model}] {Tokens} tokens in {Latency}ms",
                model, usage.TotalTokens, sw.ElapsedMilliseconds);

            return new LLMResponse
            {
                Content = content,
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
            _logger.LogError(ex, "❌ OpenRouter request failed: {Message}", ex.Message);
            return LLMResponse.Fail($"OpenRouter API error: {ex.Message}", Name);
        }
    }

    public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_settings.ApiKey)) return false;

        try
        {
            var response = await _httpClient.GetAsync("v1/models", ct);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private List<object> BuildMessages(LLMRequest request)
    {
        var messages = new List<object>();

        if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
            messages.Add(new { role = "system", content = request.SystemPrompt });

        if (request.Messages.Count > 0)
        {
            messages.AddRange(request.Messages.Select(m => new { role = m.Role, content = m.Content }));
        }
        else if (!string.IsNullOrWhiteSpace(request.Prompt))
        {
            messages.Add(new { role = "user", content = request.Prompt });
        }

        return messages;
    }
}

internal class OpenRouterChatResponse
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("choices")]
    public List<OpenRouterChoice>? Choices { get; set; }

    [JsonPropertyName("usage")]
    public OpenRouterUsage? Usage { get; set; }
}

internal class OpenRouterChoice
{
    [JsonPropertyName("message")]
    public OpenRouterMessage? Message { get; set; }

    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; set; }
}

internal class OpenRouterMessage
{
    [JsonPropertyName("role")]
    public string? Role { get; set; }

    [JsonPropertyName("content")]
    public string? Content { get; set; }
}

internal class OpenRouterUsage
{
    [JsonPropertyName("prompt_tokens")]
    public int PromptTokens { get; set; }

    [JsonPropertyName("completion_tokens")]
    public int CompletionTokens { get; set; }

    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; set; }
}
