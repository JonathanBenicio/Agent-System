using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using AgenticSystem.Core.LLM.Interfaces;
using AgenticSystem.Core.LLM.Models;
using AgenticSystem.Infrastructure.Configuration;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace AgenticSystem.Infrastructure.Vision;

/// <summary>
/// OpenAI Vision provider (gpt-4o/gpt-4o-mini com suporte a imagem).
/// </summary>
public class OpenAIVisionProvider : IVisionProvider
{
    private readonly HttpClient _httpClient;
    private readonly OpenAISettings _settings;
    private readonly ILogger<OpenAIVisionProvider> _logger;

    public OpenAIVisionProvider(HttpClient httpClient, IOptions<OpenAISettings> settings, ILogger<OpenAIVisionProvider> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;

        _httpClient.BaseAddress = new Uri(_settings.BaseUrl);
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_settings.ApiKey}");
    }

    public string Name => "OpenAI";
    public string DefaultModel => "gpt-4o-mini";
    public bool IsEnabled => _settings.Enabled && !string.IsNullOrWhiteSpace(_settings.ApiKey);
    public int Priority => _settings.Priority;

    public async Task<VisionResponse> AnalyzeImageAsync(VisionRequest request, CancellationToken ct = default)
    {
        var model = request.Model ?? DefaultModel;

        try
        {
            var imageContent = BuildImageContent(request);
            var payload = new
            {
                model,
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = new object[]
                        {
                            new { type = "text", text = request.Prompt },
                            imageContent
                        }
                    }
                },
                max_tokens = request.MaxTokens
            };

            var response = await _httpClient.PostAsJsonAsync("v1/chat/completions", payload, ct);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<VisionApiResponse>(cancellationToken: ct);

            var description = result?.Choices?.FirstOrDefault()?.Message?.Content;
            if (string.IsNullOrWhiteSpace(description))
                return VisionResponse.Fail("No vision response", Name);

            var usage = new UsageInfo
            {
                PromptTokens = result?.Usage?.PromptTokens ?? 0,
                CompletionTokens = result?.Usage?.CompletionTokens ?? 0
            };

            _logger.LogDebug("👁️ Vision [{Model}] analyzed image, {Tokens} tokens", model, usage.TotalTokens);

            return new VisionResponse
            {
                Description = description,
                Model = model,
                Provider = Name,
                Success = true,
                Usage = usage
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "❌ Vision request failed: {Message}", ex.Message);
            return VisionResponse.Fail($"Vision API error: {ex.Message}", Name);
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

    private static object BuildImageContent(VisionRequest request)
    {
        if (request.ImageBytes is { Length: > 0 })
        {
            var base64 = Convert.ToBase64String(request.ImageBytes);
            return new
            {
                type = "image_url",
                image_url = new { url = $"data:image/png;base64,{base64}" }
            };
        }

        return new
        {
            type = "image_url",
            image_url = new { url = request.ImageUrl }
        };
    }
}

internal class VisionApiResponse
{
    [JsonPropertyName("choices")]
    public List<VisionChoice>? Choices { get; set; }

    [JsonPropertyName("usage")]
    public VisionUsage? Usage { get; set; }
}

internal class VisionChoice
{
    [JsonPropertyName("message")]
    public VisionMessage? Message { get; set; }
}

internal class VisionMessage
{
    [JsonPropertyName("content")]
    public string? Content { get; set; }
}

internal class VisionUsage
{
    [JsonPropertyName("prompt_tokens")]
    public int PromptTokens { get; set; }

    [JsonPropertyName("completion_tokens")]
    public int CompletionTokens { get; set; }
}
