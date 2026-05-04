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
/// Ollama — provider local para LLMs open-source (Llama, Mistral, CodeLlama, etc.)
/// Compatible com API REST do Ollama em localhost.
/// </summary>
public class OllamaProvider : ILLMProvider
{
    private readonly HttpClient _httpClient;
    private readonly OllamaSettings _settings;
    private readonly ILogger<OllamaProvider> _logger;

    public OllamaProvider(HttpClient httpClient, IOptions<OllamaSettings> settings, ILogger<OllamaProvider> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;

        _httpClient.BaseAddress = new Uri(_settings.BaseUrl);
    }

    public string Name => "Ollama";
    public string DefaultModel => _settings.DefaultModel;
    public bool IsEnabled => _settings.Enabled;
    public int Priority => _settings.Priority;

    public void Configure(string? apiKey, string? defaultModel, bool? enabled, int? priority)
    {
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
                stream = false,
                options = new
                {
                    temperature = request.Parameters.Temperature,
                    num_predict = request.Parameters.MaxTokens,
                    top_p = request.Parameters.TopP
                }
            };

            var response = await _httpClient.PostAsJsonAsync("api/chat", payload, ct);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<OllamaChatResponse>(cancellationToken: ct);
            sw.Stop();

            if (result is null || string.IsNullOrWhiteSpace(result.Message?.Content))
                return LLMResponse.Fail("No response from Ollama", Name);

            var usage = new UsageInfo
            {
                PromptTokens = result.PromptEvalCount,
                CompletionTokens = result.EvalCount
            };

            _logger.LogDebug("🦙 Ollama [{Model}] {Tokens} tokens in {Latency}ms",
                model, usage.TotalTokens, sw.ElapsedMilliseconds);

            return new LLMResponse
            {
                Content = result.Message.Content,
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
            _logger.LogError(ex, "❌ Ollama request failed: {Message}", ex.Message);
            return LLMResponse.Fail($"Ollama API error: {ex.Message}", Name);
        }
    }

    public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        if (!IsEnabled) return false;

        try
        {
            var response = await _httpClient.GetAsync("api/tags", ct);
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

        if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
            messages.Add(new { role = "system", content = request.SystemPrompt });

        if (request.Messages.Count > 0)
            messages.AddRange(request.Messages.Select(m => new { role = m.Role, content = m.Content }));
        else if (!string.IsNullOrWhiteSpace(request.Prompt))
            messages.Add(new { role = "user", content = request.Prompt });

        return messages;
    }
}

internal class OllamaChatResponse
{
    [JsonPropertyName("message")]
    public OllamaMessage? Message { get; set; }

    [JsonPropertyName("done")]
    public bool Done { get; set; }

    [JsonPropertyName("eval_count")]
    public int EvalCount { get; set; }

    [JsonPropertyName("prompt_eval_count")]
    public int PromptEvalCount { get; set; }
}

internal class OllamaMessage
{
    [JsonPropertyName("role")]
    public string? Role { get; set; }

    [JsonPropertyName("content")]
    public string? Content { get; set; }
}
