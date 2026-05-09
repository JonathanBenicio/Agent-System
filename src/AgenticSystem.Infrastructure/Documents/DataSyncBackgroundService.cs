using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Infrastructure.Documents;

public class DataSyncBackgroundService : BackgroundService
{
    private readonly IDataConnectorManager _manager;
    private readonly ILogger<DataSyncBackgroundService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(5);

    public DataSyncBackgroundService(
        IDataConnectorManager manager,
        ILogger<DataSyncBackgroundService> logger)
    {
        _manager = manager;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("🚀 Data Sync Background Service is starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var connectors = await _manager.ListConnectorsAsync(ct: stoppingToken);
                var activeConnectors = connectors.Where(c => c.IsActive && ShouldSync(c)).ToList();

                if (activeConnectors.Any())
                {
                    _logger.LogInformation("🔄 Found {Count} active connectors for sync.", activeConnectors.Count);
                    foreach (var connector in activeConnectors)
                    {
                        if (stoppingToken.IsCancellationRequested) break;
                        await _manager.SyncConnectorAsync(connector.Id, fullSync: false, ct: stoppingToken);
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
