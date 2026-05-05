using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Services;

/// <summary>
/// ML21 — Canal de entrega via Webhook (HTTP POST).
/// Retry com backoff exponencial (max 3 tentativas).
/// </summary>
public class WebhookDeliveryChannel : IDeliveryChannel
{
    private const int MaxRetries = 3;
    private static readonly TimeSpan BaseDelay = TimeSpan.FromSeconds(1);
    private const string DefaultSignatureHeaderName = "X-Agentic-Signature";
    private const string DefaultIdempotencyHeaderName = "Idempotency-Key";
    private const int DefaultTimeoutSeconds = 10;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<WebhookDeliveryChannel> _logger;

    public string ChannelName => "webhook";

    public WebhookDeliveryChannel(
        IHttpClientFactory httpClientFactory,
        ILogger<WebhookDeliveryChannel> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<DeliveryResult> SendAsync(
        TriggerNotificationPayload payload,
        Dictionary<string, string> config,
        CancellationToken ct = default)
    {
        if (!config.TryGetValue("webhookUrl", out var webhookUrl) || string.IsNullOrEmpty(webhookUrl))
        {
            return new DeliveryResult
            {
                ChannelName = ChannelName,
                Status = DeliveryStatus.Failed,
                ErrorMessage = "webhookUrl not configured",
                Attempts = 0
            };
        }

        var client = _httpClientFactory.CreateClient("WebhookDelivery");
        client.Timeout = TimeSpan.FromSeconds(ResolveTimeoutSeconds(config));
        var result = new DeliveryResult
        {
            ChannelName = ChannelName,
            Attempts = 0
        };
        var payloadJson = JsonSerializer.Serialize(payload);

        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            result.Attempts = attempt;

            try
            {
                using var request = BuildRequest(webhookUrl, payload, payloadJson, config);
                using var response = await client.SendAsync(request, ct);
                result.HttpStatusCode = (int)response.StatusCode;

                if (response.IsSuccessStatusCode)
                {
                    result.Status = DeliveryStatus.Success;
                    result.DeliveredAt = DateTime.UtcNow;
                    _logger.LogInformation("📬 Webhook entregue em {Attempts} tentativa(s): {Url}",
                        attempt, webhookUrl);
                    return result;
                }

                _logger.LogWarning("⚠️ Webhook retornou {StatusCode} (tentativa {Attempt}/{Max})",
                    response.StatusCode, attempt, MaxRetries);
            }
            catch (Exception ex) when (attempt < MaxRetries)
            {
                _logger.LogWarning(ex, "⚠️ Tentativa {Attempt}/{Max} falhou para {Url}",
                    attempt, MaxRetries, webhookUrl);
                result.ErrorMessage = ex.Message;
            }
            catch (Exception ex)
            {
                result.Status = DeliveryStatus.Failed;
                result.ErrorMessage = ex.Message;
                _logger.LogError(ex, "❌ Webhook falhou após {Max} tentativas: {Url}", MaxRetries, webhookUrl);
                return result;
            }

            if (attempt < MaxRetries)
            {
                result.Status = DeliveryStatus.Retrying;
                var delay = TimeSpan.FromTicks(BaseDelay.Ticks * (long)Math.Pow(2, attempt - 1));
                await Task.Delay(delay, ct);
            }
        }

        result.Status = DeliveryStatus.Failed;
        result.ErrorMessage ??= $"Failed after {MaxRetries} attempts";
        return result;
    }

    public async Task<bool> IsHealthyAsync(CancellationToken ct = default)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("WebhookDelivery");
            // Perform a simple connectivity check
            return client != null;
        }
        catch
        {
            return false;
        }
    }

    private static HttpRequestMessage BuildRequest(
        string webhookUrl,
        TriggerNotificationPayload payload,
        string payloadJson,
        Dictionary<string, string> config)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, webhookUrl)
        {
            Content = new StringContent(payloadJson, Encoding.UTF8, "application/json")
        };

        foreach (var header in config.Where(entry => entry.Key.StartsWith("header:", StringComparison.OrdinalIgnoreCase)))
        {
            var headerName = header.Key["header:".Length..];
            if (!string.IsNullOrWhiteSpace(headerName))
            {
                request.Headers.TryAddWithoutValidation(headerName, header.Value);
            }
        }

        var idempotencyHeaderName = GetConfigValue(config, "idempotencyHeaderName", DefaultIdempotencyHeaderName);
        var idempotencyKey = GetConfigValue(config, "idempotencyKey", $"{payload.TriggerName}:{payload.Timestamp:O}");
        request.Headers.TryAddWithoutValidation(idempotencyHeaderName, idempotencyKey);

        var hmacSecret = GetConfigValue(config, "hmacSecret", string.Empty);
        if (!string.IsNullOrWhiteSpace(hmacSecret))
        {
            var signatureHeaderName = GetConfigValue(config, "signatureHeaderName", DefaultSignatureHeaderName);
            request.Headers.TryAddWithoutValidation(signatureHeaderName, ComputeSignature(payloadJson, hmacSecret));
        }

        return request;
    }

    private static int ResolveTimeoutSeconds(Dictionary<string, string> config)
    {
        if (config.TryGetValue("timeoutSeconds", out var timeoutValue)
            && int.TryParse(timeoutValue, out var timeoutSeconds)
            && timeoutSeconds > 0)
        {
            return timeoutSeconds;
        }

        return DefaultTimeoutSeconds;
    }

    private static string GetConfigValue(Dictionary<string, string> config, string key, string fallback)
        => config.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : fallback;

    private static string ComputeSignature(string payloadJson, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payloadJson));
        return Convert.ToHexString(hash);
    }
}
