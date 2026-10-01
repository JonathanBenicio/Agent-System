using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Core.Services;

/// <summary>
/// Background service that periodically checks for expired secrets
/// and marks them with PendingRotation status, notifying via audit log.
/// </summary>
public class SecretRotationBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SecretRotationBackgroundService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromHours(1);
    private readonly TimeSpan _lookaheadWindow = TimeSpan.FromDays(7);

    public SecretRotationBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<SecretRotationBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
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
        using var scope = _serviceProvider.CreateScope();
        var tenantAccessor = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
        var systemOperations = scope.ServiceProvider.GetRequiredService<ISystemOperationContextAccessor>();
        var tenantStore = scope.ServiceProvider.GetRequiredService<ITenantStore>();
        var configManager = scope.ServiceProvider.GetRequiredService<IConfigManager>();
        var auditLog = scope.ServiceProvider.GetRequiredService<IAuditLog>();

        IReadOnlyList<Models.Tenant> tenants;
        using (systemOperations.BeginScope(SystemOperationKind.EnumerateTenantsForSecretRotation))
        {
            systemOperations.Require(SystemOperationKind.EnumerateTenantsForSecretRotation);
            tenants = await tenantStore.GetAllAsync(ct);
        }

        foreach (var tenant in tenants)
        {
            using var tenantScope = tenantAccessor.BeginScope(new Models.TenantContext { TenantId = tenant.Id });
            var expiredSecrets = await configManager.GetExpiredSecretsAsync(_lookaheadWindow);
            var expiredList = expiredSecrets.ToList();
            if (expiredList.Count == 0) continue;

            _logger.LogWarning("Found {Count} secrets expiring within {Window} days for tenant {TenantId}",
                expiredList.Count, _lookaheadWindow.TotalDays, tenant.Id);

            foreach (var secret in expiredList)
            {
                var isAlreadyExpired = secret.ExpiresAt.HasValue && secret.ExpiresAt.Value < DateTime.UtcNow;

                await auditLog.RecordAsync(new Models.AuditEntry
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
}
