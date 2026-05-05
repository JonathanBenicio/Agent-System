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
/// Anthropic Claude — provider via Messages API.
/// </summary>
public class ClaudeProvider : ILLMProvider
{
    private readonly HttpClient _httpClient;
    private readonly ClaudeSettings _settings;
    private readonly ILogger<ClaudeProvider> _logger;

    public ClaudeProvider(HttpClient httpClient, IOptions<ClaudeSettings> settings, ILogger<ClaudeProvider> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;

        _httpClient.BaseAddress = new Uri(_settings.BaseUrl);
        _httpClient.DefaultRequestHeaders.Add("x-api-key", _settings.ApiKey);
        _httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
    }

    public string Name => "Claude";
    public string DefaultModel => _settings.DefaultModel;
    public bool IsEnabled => _settings.Enabled && !string.IsNullOrWhiteSpace(_settings.ApiKey);
    public int Priority => _settings.Priority;

    public void Configure(string? apiKey, string? defaultModel, bool? enabled, int? priority)
    {
        if (apiKey is not null)
        {
            _settings.ApiKey = apiKey;
            _httpClient.DefaultRequestHeaders.Remove("x-api-key");
            _httpClient.DefaultRequestHeaders.Add("x-api-key", _settings.ApiKey);
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
            var payload = new Dictionary<string, object>
            {
                ["model"] = model,
                ["messages"] = messages,
                ["max_tokens"] = request.Parameters.MaxTokens
            };

            if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
                payload["system"] = request.SystemPrompt;

            if (request.Parameters.Temperature > 0)
                payload["temperature"] = request.Parameters.Temperature;

            if (request.Parameters.TopP > 0)
                payload["top_p"] = request.Parameters.TopP;

            var response = await _httpClient.PostAsJsonAsync("v1/messages", payload, ct);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<ClaudeResponse>(cancellationToken: ct);
            sw.Stop();

            var text = result?.Content?.FirstOrDefault()?.Text;
            if (string.IsNullOrWhiteSpace(text))
                return LLMResponse.Fail("No content from Claude", Name);

            var usage = new UsageInfo
            {
                PromptTokens = result?.Usage?.InputTokens ?? 0,
                CompletionTokens = result?.Usage?.OutputTokens ?? 0
            };

            _logger.LogDebug("🟣 Claude [{Model}] {Tokens} tokens in {Latency}ms",
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
            _logger.LogError(ex, "❌ Claude request failed: {Message}", ex.Message);
            return LLMResponse.Fail($"Claude API error: {ex.Message}", Name);
        }
    }

    public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        if (!IsEnabled) return false;

        try
        {
            // Claude doesn't have a simple health endpoint; try a minimal request
            var payload = new
            {
                model = DefaultModel,
                messages = new[] { new { role = "user", content = "ping" } },
                max_tokens = 1
            };
            var response = await _httpClient.PostAsJsonAsync("v1/messages", payload, ct);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private static List<object> BuildMessages(LLMRequest request)
    {
        var messages = new List<object>();

        if (request.Messages.Count > 0)
        {
            // Claude doesn't accept system role in messages array
            messages.AddRange(request.Messages
                .Where(m => m.Role != "system")
                .Select(m => new { role = m.Role, content = m.Content }));
        }
        else if (!string.IsNullOrWhiteSpace(request.Prompt))
        {
            messages.Add(new { role = "user", content = request.Prompt });
        }

        return messages;
    }
}

// Claude API response models
internal class ClaudeResponse
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("content")]
    public List<ClaudeContentBlock>? Content { get; set; }

    [JsonPropertyName("usage")]
    public ClaudeUsage? Usage { get; set; }

    [JsonPropertyName("stop_reason")]
    public string? StopReason { get; set; }
}

internal class ClaudeContentBlock
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("text")]
    public string? Text { get; set; }
}

internal class ClaudeUsage
{
    [JsonPropertyName("input_tokens")]
    public int InputTokens { get; set; }

    [JsonPropertyName("output_tokens")]
    public int OutputTokens { get; set; }
}
