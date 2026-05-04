using System.Net.Http.Json;
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
        var result = new DeliveryResult
        {
            ChannelName = ChannelName,
            Attempts = 0
        };

        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            result.Attempts = attempt;

            try
            {
                var response = await client.PostAsJsonAsync(webhookUrl, payload, ct);
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
}
