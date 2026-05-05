using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using AgenticSystem.Core.Interfaces;

namespace AgenticSystem.Core.Services;

/// <summary>
/// GAP-13 — BackgroundService que periodicamente limpa agents inativos.
/// </summary>
public class AgentCleanupHostedService : BackgroundService
{
    private readonly IMetaAgent _metaAgent;
    private readonly ILogger<AgentCleanupHostedService> _logger;
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromMinutes(5);

    public AgentCleanupHostedService(
        IMetaAgent metaAgent,
        ILogger<AgentCleanupHostedService> logger)
    {
        _metaAgent = metaAgent;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AgentCleanupHostedService started (interval: {Interval})", CleanupInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _metaAgent.CleanupInactiveAgentsAsync();
                _logger.LogDebug("🧹 Agent cleanup tick completed");
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
