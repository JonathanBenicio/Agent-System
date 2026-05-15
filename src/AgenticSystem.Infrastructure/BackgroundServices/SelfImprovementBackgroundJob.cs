using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgenticSystem.Infrastructure.BackgroundServices;

/// <summary>
/// Job que executa o ciclo de auto-melhoria (Self-Improvement) uma vez por dia.
/// </summary>
public class SelfImprovementBackgroundJob : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SelfImprovementBackgroundJob> _logger;
    private readonly SelfImprovementSettings _settings;
    private readonly TimeSpan _runInterval;

    public SelfImprovementBackgroundJob(
        IServiceProvider serviceProvider,
        IOptions<SelfImprovementSettings> options,
        ILogger<SelfImprovementBackgroundJob> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _settings = options.Value;
        _runInterval = TimeSpan.FromHours(_settings.RunIntervalHours);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_settings.Enabled)
        {
            _logger.LogInformation("ℹ️ Self-Improvement Background Job is disabled by configuration.");
            return;
        }

        _logger.LogInformation("🚀 Self-Improvement Background Job initialized. Interval: {Interval}", _runInterval);

        // Aguarda um pouco antes da primeira execução para garantir que o sistema subiu totalmente
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

        using var timer = new PeriodicTimer(_runInterval);

        try
        {
            // Executa a primeira vez imediatamente (após o delay inicial)
            await RunCycleAsync(stoppingToken);

            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await RunCycleAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("🛑 Self-Improvement Background Job is stopping.");
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "❌ Fatal error in Self-Improvement Background Job.");
        }
    }

    private async Task RunCycleAsync(CancellationToken ct)
    {
        _logger.LogInformation("🔄 Executing Self-Improvement Cycle: {Time}", DateTime.UtcNow);

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var improvementService = scope.ServiceProvider.GetRequiredService<ISelfImprovementEngine>();

            await improvementService.ProcessBatchImprovementsAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error during self-improvement batch cycle execution.");
        }
    }
}
