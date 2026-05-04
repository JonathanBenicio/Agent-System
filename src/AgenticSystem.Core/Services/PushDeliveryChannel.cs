using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Services;

/// <summary>
/// ML21 — Canal de entrega por Push Notification (Firebase Cloud Messaging / APNS).
/// Config keys: fcmServerKey, fcmUrl, deviceToken, title (optional)
/// </summary>
public class PushDeliveryChannel : IDeliveryChannel
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<PushDeliveryChannel> _logger;

    private const string DefaultFcmUrl = "https://fcm.googleapis.com/fcm/send";

    public PushDeliveryChannel(IHttpClientFactory httpClientFactory, ILogger<PushDeliveryChannel> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public string ChannelName => "push";

    public async Task<DeliveryResult> SendAsync(
        TriggerNotificationPayload payload,
        Dictionary<string, string> config,
        CancellationToken ct = default)
    {
        var result = new DeliveryResult
        {
            ChannelName = ChannelName,
            Status = DeliveryStatus.Failed,
            Attempts = 0
        };

        if (!config.TryGetValue("fcmServerKey", out var serverKey) ||
            !config.TryGetValue("deviceToken", out var deviceToken))
        {
            result.ErrorMessage = "Missing required config: fcmServerKey, deviceToken";
            return result;
        }

        var fcmUrl = config.TryGetValue("fcmUrl", out var url) ? url : DefaultFcmUrl;
        var title = config.TryGetValue("title", out var t) ? t : $"Trigger: {payload.TriggerName}";

        var fcmPayload = new
        {
            to = deviceToken,
            notification = new
            {
                title,
                body = $"{payload.ConditionResult} | {payload.SuggestedAction ?? "Verifique o sistema"}"
            },
            data = new
            {
                triggerName = payload.TriggerName,
                timestamp = payload.Timestamp.ToString("o"),
                actualValue = payload.ActualValue,
                expectedValue = payload.ExpectedValue
            }
        };

        const int maxRetries = 3;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            result.Attempts = attempt;

            try
            {
                using var client = _httpClientFactory.CreateClient("push-notifications");
                using var request = new HttpRequestMessage(HttpMethod.Post, fcmUrl);
                request.Headers.TryAddWithoutValidation("Authorization", $"key={serverKey}");
                request.Content = JsonContent.Create(fcmPayload);

                var response = await client.SendAsync(request, ct);
                result.HttpStatusCode = (int)response.StatusCode;

                if (response.IsSuccessStatusCode)
                {
                    result.Status = DeliveryStatus.Success;
                    result.DeliveredAt = DateTime.UtcNow;
                    _logger.LogInformation("Push notification sent for trigger {Trigger}", payload.TriggerName);
                    return result;
                }

                var body = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("Push delivery attempt {Attempt} returned {Status}: {Body}",
                    attempt, response.StatusCode, body);

                if (attempt == maxRetries)
                {
                    result.ErrorMessage = $"HTTP {response.StatusCode}: {body}";
                    result.Status = DeliveryStatus.Failed;
                }
                else
                {
                    result.Status = DeliveryStatus.Retrying;
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), ct);
                }
            }
            catch (Exception ex) when (attempt < maxRetries)
            {
                _logger.LogWarning(ex, "Push delivery attempt {Attempt}/{Max} failed", attempt, maxRetries);
                result.Status = DeliveryStatus.Retrying;
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Push delivery failed after {Max} attempts", maxRetries);
                result.ErrorMessage = ex.Message;
                result.Status = DeliveryStatus.Failed;
            }
        }

        return result;
    }

    public async Task<bool> IsHealthyAsync(CancellationToken ct = default)
    {
        try
        {
            using var client = _httpClientFactory.CreateClient("push-notifications");
            var response = await client.GetAsync("https://fcm.googleapis.com", ct);
            return response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.NotFound;
        }
        catch
        {
            return false;
        }
    }
}
