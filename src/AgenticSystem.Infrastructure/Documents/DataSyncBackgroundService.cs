using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Infrastructure.Documents;

public class DataSyncBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DataSyncBackgroundService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(5);

    public DataSyncBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<DataSyncBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("🚀 Data Sync Background Service is starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var manager = scope.ServiceProvider.GetRequiredService<IDataConnectorManager>();
                var tenantStore = scope.ServiceProvider.GetRequiredService<ITenantStore>();
                var tenantContextAccessor = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();

                IReadOnlyList<Tenant> allTenants;
                try
                {
                    allTenants = await tenantStore.GetAllAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to enumerate tenants for data sync; skipping this cycle.");
                    continue;
                }

                foreach (var tenantId in allTenants.Select(tenant => tenant.Id))
                {
                    using var tenantScope = tenantContextAccessor.BeginScope(new TenantContext { TenantId = tenantId });
                    
                    try
                    {
                        var connectors = await manager.ListConnectorsAsync(ct: stoppingToken);
                        var activeConnectors = connectors.Where(c => c.IsActive && ShouldSync(c)).ToList();

                        if (activeConnectors.Any())
                        {
                            _logger.LogInformation("🔄 Found {Count} active connectors for sync for tenant {TenantId}.", activeConnectors.Count, tenantId);
                            foreach (var connector in activeConnectors)
                            {
                                if (stoppingToken.IsCancellationRequested) break;
                                await manager.SyncConnectorAsync(connector.Id, fullSync: false, ct: stoppingToken);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "🚨 Error syncing connectors for tenant {TenantId}", tenantId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🚨 Error in Data Sync background cycle.");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }

        _logger.LogInformation("⏹️ Data Sync Background Service is stopping.");
    }

    private bool ShouldSync(DataConnectorConfig config)
    {
        if (config.LastSyncAt == null) return true;

        var nextSync = config.LastSyncAt.Value.Add(config.SyncSchedule.SyncInterval);
        return DateTime.UtcNow >= nextSync;
    }
}
