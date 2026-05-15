using Microsoft.Extensions.Logging;
using AgenticSystem.Core.Interfaces;
using System.Net.Http.Headers;

namespace AgenticSystem.Infrastructure.LLM.Handlers;

/// <summary>
/// Intercepts HTTP responses from LLM providers to capture rate limit headers.
/// </summary>
public class ExternalQuotaHeaderHandler : DelegatingHandler
{
    private readonly IExternalQuotaSyncService _quotaService;
    private readonly ILogger<ExternalQuotaHeaderHandler> _logger;

    public ExternalQuotaHeaderHandler(
        IExternalQuotaSyncService quotaService,
        ILogger<ExternalQuotaHeaderHandler> logger)
    {
        _quotaService = quotaService;
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);

        try
        {
            await ProcessHeadersAsync(request, response);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to process rate limit headers from {Uri}", request.RequestUri);
        }

        return response;
    }

    private async Task ProcessHeadersAsync(HttpRequestMessage request, HttpResponseMessage response)
    {
        // Try to identify provider and metadata from headers (set by the provider class)
        var providerName = GetProviderName(request);
        if (string.IsNullOrEmpty(providerName)) return;

        string? apiKeyId = null;
        if (request.Headers.TryGetValues("X-Agentic-ApiKeyId", out var keyIds))
            apiKeyId = keyIds.FirstOrDefault();

        if (string.IsNullOrEmpty(apiKeyId)) return;

        string? tenantId = null;
        if (request.Headers.TryGetValues("X-Agentic-TenantId", out var tenantIds))
            tenantId = tenantIds.FirstOrDefault();

        // Clean up internal headers so they don't leak to the external API
        request.Headers.Remove("X-Agentic-ProviderName");
        request.Headers.Remove("X-Agentic-ApiKeyId");
        request.Headers.Remove("X-Agentic-TenantId");

        long limitRequests = -1;
        long remainingRequests = -1;
        long limitTokens = -1;
        long remainingTokens = -1;
        DateTime? resetAt = null;

        if (providerName.Equals("OpenAI", StringComparison.OrdinalIgnoreCase) || 
            providerName.Equals("OpenRouter", StringComparison.OrdinalIgnoreCase))
        {
            limitRequests = GetHeaderValue(response.Headers, "x-ratelimit-limit-requests");
            remainingRequests = GetHeaderValue(response.Headers, "x-ratelimit-remaining-requests");
            limitTokens = GetHeaderValue(response.Headers, "x-ratelimit-limit-tokens");
            remainingTokens = GetHeaderValue(response.Headers, "x-ratelimit-remaining-tokens");
        }
        else if (providerName.Equals("Claude", StringComparison.OrdinalIgnoreCase))
        {
            limitRequests = GetHeaderValue(response.Headers, "anthropic-ratelimit-requests-limit");
            remainingRequests = GetHeaderValue(response.Headers, "anthropic-ratelimit-requests-remaining");
            limitTokens = GetHeaderValue(response.Headers, "anthropic-ratelimit-tokens-limit");
            remainingTokens = GetHeaderValue(response.Headers, "anthropic-ratelimit-tokens-remaining");
        }
        else if (providerName.Equals("Gemini", StringComparison.OrdinalIgnoreCase))
        {
            limitRequests = GetHeaderValue(response.Headers, "x-ratelimit-limit-requests");
            remainingRequests = GetHeaderValue(response.Headers, "x-ratelimit-remaining-requests");
        }

        if (remainingRequests != -1 || remainingTokens != -1)
        {
            await _quotaService.UpdateFromHeadersAsync(
                providerName,
                tenantId,
                apiKeyId,
                limitRequests,
                remainingRequests,
                limitTokens,
                remainingTokens,
                resetAt);
        }
    }

    private string? GetProviderName(HttpRequestMessage request)
    {
        if (request.Headers.TryGetValues("X-Agentic-ProviderName", out var names))
            return names.FirstOrDefault();

        var host = request.RequestUri?.Host ?? "";
        if (host.Contains("openai.com")) return "OpenAI";
        if (host.Contains("anthropic.com")) return "Claude";
        if (host.Contains("googleapis.com")) return "Gemini";
        if (host.Contains("openrouter.ai")) return "OpenRouter";

        return null;
    }

    private long GetHeaderValue(HttpResponseHeaders headers, string name)
    {
        if (headers.TryGetValues(name, out var values))
        {
            var value = values.FirstOrDefault();
            if (long.TryParse(value, out var result))
                return result;
        }
        return -1;
    }
}
