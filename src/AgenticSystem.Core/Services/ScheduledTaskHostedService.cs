using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Services;

/// <summary>
/// ML21 — BackgroundService que tick a cada minuto, executando tarefas agendadas cujo NextRunAt já passou.
/// </summary>
public class ScheduledTaskHostedService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ScheduledTaskHostedService> _logger;
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(30);

    public ScheduledTaskHostedService(
        IServiceProvider serviceProvider,
        ILogger<ScheduledTaskHostedService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ScheduledTaskHostedService started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await TickAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in scheduled task tick");
            }

            await Task.Delay(TickInterval, stoppingToken);
        }

        _logger.LogInformation("ScheduledTaskHostedService stopped");
    }

    private async Task TickAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var taskManager = scope.ServiceProvider.GetRequiredService<IScheduledTaskManager>();

        var activeTasks = await taskManager.GetActiveAsync(ct);
        var now = DateTime.UtcNow;

        foreach (var task in activeTasks)
        {
            if (task.NextRunAt.HasValue && task.NextRunAt.Value <= now)
            {
                _logger.LogDebug("Executing due task {TaskId} ({TaskName})", task.Id, task.Name);

                try
                {
                    await taskManager.ExecuteAsync(task.Id, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Task {TaskId} execution failed", task.Id);
                }
            }
        }
    }
}
