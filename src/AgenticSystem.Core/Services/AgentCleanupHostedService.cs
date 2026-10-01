using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using AgenticSystem.Core.Interfaces;

namespace AgenticSystem.Core.Services;

/// <summary>
/// GAP-13 — BackgroundService que periodicamente limpa agents inativos.
/// </summary>
public class AgentCleanupHostedService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AgentCleanupHostedService> _logger;
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromMinutes(5);

    public AgentCleanupHostedService(
        IServiceProvider serviceProvider,
        ILogger<AgentCleanupHostedService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AgentCleanupHostedService started (interval: {Interval})", CleanupInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var metaAgent = scope.ServiceProvider.GetRequiredService<IMetaAgent>();
                var tenantStore = scope.ServiceProvider.GetRequiredService<ITenantStore>();
                var tenantContextAccessor = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();

                IReadOnlyList<AgenticSystem.Core.Models.Tenant> allTenants;
                try
                {
                    allTenants = await tenantStore.GetAllAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to enumerate tenants for agent cleanup; skipping this cycle.");
                    continue;
                }

                foreach (var tenantId in allTenants.Select(tenant => tenant.Id))
                {
                    using var tenantScope = tenantContextAccessor.BeginScope(new AgenticSystem.Core.Models.TenantContext { TenantId = tenantId });
                    try
                    {
                        await metaAgent.CleanupInactiveAgentsAsync();
                        _logger.LogDebug("🧹 Agent cleanup tick completed for tenant {TenantId}", tenantId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error during agent cleanup for tenant {TenantId}", tenantId);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during agent cleanup");
            }

            await Task.Delay(CleanupInterval, stoppingToken);
        }

        _logger.LogInformation("AgentCleanupHostedService stopped");
    }
}
