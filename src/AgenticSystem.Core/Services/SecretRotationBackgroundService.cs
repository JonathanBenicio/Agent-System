using AgenticSystem.Core.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Core.Services;

/// <summary>
/// Background service that periodically checks for expired secrets
/// and marks them with PendingRotation status, notifying via audit log.
/// </summary>
public class SecretRotationBackgroundService : BackgroundService
{
    private readonly IConfigManager _configManager;
    private readonly IAuditLog _auditLog;
    private readonly ILogger<SecretRotationBackgroundService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromHours(1);
    private readonly TimeSpan _lookaheadWindow = TimeSpan.FromDays(7);

    public SecretRotationBackgroundService(
        IConfigManager configManager,
        IAuditLog auditLog,
        ILogger<SecretRotationBackgroundService> logger)
    {
        _configManager = configManager;
        _auditLog = auditLog;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SecretRotationBackgroundService started. Check interval: {Interval}", _checkInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckExpiredSecretsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking for expired secrets");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }
    }

    private async Task CheckExpiredSecretsAsync(CancellationToken ct)
    {
        var expiredSecrets = await _configManager.GetExpiredSecretsAsync(_lookaheadWindow);
        var expiredList = expiredSecrets.ToList();

        if (expiredList.Count == 0) return;

        _logger.LogWarning("Found {Count} secrets expiring within {Window} days", expiredList.Count, _lookaheadWindow.TotalDays);

        foreach (var secret in expiredList)
        {
            var isAlreadyExpired = secret.ExpiresAt.HasValue && secret.ExpiresAt.Value < DateTime.UtcNow;

            await _auditLog.RecordAsync(new Models.AuditEntry
            {
                Category = Models.AuditCategory.Security,
                Action = isAlreadyExpired ? "SecretExpired" : "SecretExpiringSoon",
                Description = isAlreadyExpired
                    ? $"Secret '{secret.Key}' has expired on {secret.ExpiresAt:u}. Immediate rotation required."
                    : $"Secret '{secret.Key}' will expire on {secret.ExpiresAt:u}. Rotation recommended.",
                Metadata = new Dictionary<string, object>
                {
                    ["configKey"] = secret.Key,
                    ["category"] = secret.Category.ToString(),
                    ["provider"] = secret.Provider ?? "unknown",
                    ["expiresAt"] = secret.ExpiresAt?.ToString("u") ?? "N/A",
                    ["severity"] = isAlreadyExpired ? "critical" : "warning"
                }
            }, ct);
        }
    }
}
