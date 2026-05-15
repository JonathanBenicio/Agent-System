using System.Net.Http.Headers;
using AgenticSystem.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Infrastructure.LLM.Handlers;

/// <summary>
/// HttpClient handler that intercepts LLM provider responses to extract rate limit headers.
/// </summary>
public class LLMQuotaInterceptorHandler : DelegatingHandler
{
    private readonly IExternalQuotaSyncService _quotaService;
    private readonly string _providerName;
    private readonly ILogger<LLMQuotaInterceptorHandler> _logger;

    public LLMQuotaInterceptorHandler(
        IExternalQuotaSyncService quotaService,
        string providerName,
        ILogger<LLMQuotaInterceptorHandler> logger)
    {
        _quotaService = quotaService;
        _providerName = providerName;
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);

        try
        {
            await ProcessHeadersAsync(response.Headers);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to process rate limit headers for {Provider}", _providerName);
        }

        return response;
    }

    private async Task ProcessHeadersAsync(HttpResponseHeaders headers)
    {
        long limitRequests = -1;
        long remainingRequests = -1;
        long limitTokens = -1;
        long remainingTokens = -1;
        DateTime? resetAt = null;

        if (_providerName.Equals("OpenAI", StringComparison.OrdinalIgnoreCase))
        {
            limitRequests = GetHeaderValue(headers, "x-ratelimit-limit-requests");
            remainingRequests = GetHeaderValue(headers, "x-ratelimit-remaining-requests");
            limitTokens = GetHeaderValue(headers, "x-ratelimit-limit-tokens");
            remainingTokens = GetHeaderValue(headers, "x-ratelimit-remaining-tokens");
            resetAt = ParseResetTime(headers, "x-ratelimit-reset-requests");
        }
        else if (_providerName.Equals("Claude", StringComparison.OrdinalIgnoreCase) || _providerName.Equals("Anthropic", StringComparison.OrdinalIgnoreCase))
        {
            limitRequests = GetHeaderValue(headers, "anthropic-ratelimit-requests-limit");
            remainingRequests = GetHeaderValue(headers, "anthropic-ratelimit-requests-remaining");
            limitTokens = GetHeaderValue(headers, "anthropic-ratelimit-tokens-limit");
            remainingTokens = GetHeaderValue(headers, "anthropic-ratelimit-tokens-remaining");
            resetAt = ParseResetTime(headers, "anthropic-ratelimit-requests-reset");
        }
        else if (_providerName.Equals("Gemini", StringComparison.OrdinalIgnoreCase) || _providerName.Equals("Google", StringComparison.OrdinalIgnoreCase))
        {
            limitRequests = GetHeaderValue(headers, "x-ratelimit-limit-requests");
            remainingRequests = GetHeaderValue(headers, "x-ratelimit-remaining-requests");
            limitTokens = GetHeaderValue(headers, "x-ratelimit-limit-tokens");
            remainingTokens = GetHeaderValue(headers, "x-ratelimit-remaining-tokens");
            resetAt = ParseResetTime(headers, "x-ratelimit-reset-requests");
        }
        else if (_providerName.Equals("OpenRouter", StringComparison.OrdinalIgnoreCase))
        {
            limitRequests = GetHeaderValue(headers, "x-ratelimit-limit-requests");
            remainingRequests = GetHeaderValue(headers, "x-ratelimit-remaining-requests");
            limitTokens = GetHeaderValue(headers, "x-ratelimit-limit-tokens");
            remainingTokens = GetHeaderValue(headers, "x-ratelimit-remaining-tokens");
            resetAt = ParseResetTime(headers, "x-ratelimit-reset-requests");
        }

        if (remainingRequests != -1 || remainingTokens != -1)
        {
            // We need the API Key ID. For now, we'll use a placeholder or extract from Auth header if possible.
            // Ideally, the handler should have the key context injected or passed via request options.
            // For this implementation, we'll use "default_key" or similar if we can't find it.
            string apiKeyId = ExtractApiKeyHash(headers) ?? "default_key";

            await _quotaService.UpdateFromHeadersAsync(_providerName, null, apiKeyId, limitRequests, remainingRequests, limitTokens, remainingTokens, resetAt);
        }
    }

    private long GetHeaderValue(HttpResponseHeaders headers, string name)
    {
        if (headers.TryGetValues(name, out var values))
        {
            if (long.TryParse(values.FirstOrDefault(), out var result))
            {
                return result;
            }
        }
        return -1;
    }

    private DateTime? ParseResetTime(HttpResponseHeaders headers, string name)
    {
        if (headers.TryGetValues(name, out var values))
        {
            var value = values.FirstOrDefault();
            if (string.IsNullOrEmpty(value)) return null;

            // Many providers return "60s" or "2ms" or just seconds
            if (value.EndsWith("s"))
            {
                if (double.TryParse(value.TrimEnd('s'), out var seconds))
                    return DateTime.UtcNow.AddSeconds(seconds);
            }
            else if (double.TryParse(value, out var seconds))
            {
                return DateTime.UtcNow.AddSeconds(seconds);
            }
        }
        return null;
    }

    private string? ExtractApiKeyHash(HttpResponseHeaders headers)
    {
        // Headers usually don't contain the API key for security.
        // The key is in the Request message.
        return null; 
    }
}
