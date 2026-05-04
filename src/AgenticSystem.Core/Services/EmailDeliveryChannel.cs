using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Services;

/// <summary>
/// ML21 — Canal de entrega por email (SMTP/SendGrid).
/// Config keys: smtpHost, smtpPort, smtpUser, smtpPassword, fromAddress, toAddress, useSsl
/// </summary>
public class EmailDeliveryChannel : IDeliveryChannel
{
    private readonly ILogger<EmailDeliveryChannel> _logger;

    public EmailDeliveryChannel(ILogger<EmailDeliveryChannel> logger)
    {
        _logger = logger;
    }

    public string ChannelName => "email";

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

        if (!config.TryGetValue("smtpHost", out var host) ||
            !config.TryGetValue("fromAddress", out var from) ||
            !config.TryGetValue("toAddress", out var to))
        {
            result.ErrorMessage = "Missing required config: smtpHost, fromAddress, toAddress";
            return result;
        }

        var port = config.TryGetValue("smtpPort", out var portStr) && int.TryParse(portStr, out var p) ? p : 587;
        var useSsl = !config.TryGetValue("useSsl", out var sslStr) || !bool.TryParse(sslStr, out var ssl) || ssl;

        var subject = $"[Trigger] {payload.TriggerName} — {payload.Timestamp:u}";
        var body = BuildEmailBody(payload);

        const int maxRetries = 3;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            result.Attempts = attempt;

            try
            {
                using var client = new SmtpClient(host, port)
                {
                    EnableSsl = useSsl,
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    Timeout = 30_000
                };

                if (config.TryGetValue("smtpUser", out var user) &&
                    config.TryGetValue("smtpPassword", out var password))
                {
                    client.Credentials = new NetworkCredential(user, password);
                }

                var message = new MailMessage(from, to, subject, body)
                {
                    IsBodyHtml = false
                };

                await client.SendMailAsync(message, ct);

                result.Status = DeliveryStatus.Success;
                result.DeliveredAt = DateTime.UtcNow;
                _logger.LogInformation("Email sent to {To} for trigger {Trigger}", to, payload.TriggerName);
                return result;
            }
            catch (Exception ex) when (attempt < maxRetries)
            {
                _logger.LogWarning(ex, "Email delivery attempt {Attempt}/{Max} failed", attempt, maxRetries);
                result.Status = DeliveryStatus.Retrying;
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Email delivery failed after {Max} attempts", maxRetries);
                result.ErrorMessage = ex.Message;
                result.Status = DeliveryStatus.Failed;
            }
        }

        return result;
    }

    public Task<bool> IsHealthyAsync(CancellationToken ct = default)
    {
        // Basic health: we can't test SMTP connection without credentials,
        // so return true (available) — actual delivery validates connectivity.
        return Task.FromResult(true);
    }

    private static string BuildEmailBody(TriggerNotificationPayload payload)
    {
        return $"""
            Trigger: {payload.TriggerName}
            Timestamp: {payload.Timestamp:u}
            Condition Result: {payload.ConditionResult}
            Actual Value: {payload.ActualValue ?? "N/A"}
            Expected Value: {payload.ExpectedValue ?? "N/A"}
            Suggested Action: {payload.SuggestedAction ?? "N/A"}
            
            Metadata:
            {string.Join(Environment.NewLine, payload.Metadata.Select(kv => $"  {kv.Key}: {kv.Value}"))}
            """;
    }
}
