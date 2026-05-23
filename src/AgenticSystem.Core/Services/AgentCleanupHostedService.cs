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

                await metaAgent.CleanupInactiveAgentsAsync();
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
