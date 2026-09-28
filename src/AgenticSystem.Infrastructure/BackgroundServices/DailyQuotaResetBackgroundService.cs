using AgenticSystem.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Infrastructure.BackgroundServices;

/// <summary>
/// Hosted service that triggers a daily reset of tenant quota counters.
/// Runs at 00:01 UTC each day to reset <c>CurrentDailyTokens</c>,
/// <c>CurrentDailyCostUsd</c>, and <c>CurrentDailyRequests</c> in the database.
///
/// Design: A long-running <see cref="BackgroundService"/> that sleeps until the
/// next UTC midnight rather than a fixed interval, so the reset always aligns
/// with the start of a UTC day regardless of when the service started.
/// </summary>
public sealed class DailyQuotaResetBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DailyQuotaResetBackgroundService> _logger;

    public DailyQuotaResetBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<DailyQuotaResetBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DailyQuotaResetBackgroundService started. First reset at {NextReset:O}", NextMidnightUtc());

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = NextMidnightUtc() - DateTime.UtcNow;
            if (delay > TimeSpan.Zero)
            {
                try
                {
                    await Task.Delay(delay, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }

            if (stoppingToken.IsCancellationRequested) break;

            await ResetQuotasAsync(stoppingToken);
        }
    }

    private async Task ResetQuotasAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var repository = scope.ServiceProvider.GetRequiredService<ITenantQuotaRepository>();
            await repository.ResetDailyCountersAsync(ct);
            _logger.LogInformation("Daily quota counters reset at {UtcNow:O}", DateTime.UtcNow);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogError(ex, "Failed to reset daily quota counters. Will retry tomorrow.");
        }
    }

    private static DateTime NextMidnightUtc()
    {
        var now = DateTime.UtcNow;
        // Add 1 minute buffer so we don't fire fractionally early due to timer drift.
        return now.Date.AddDays(1).AddMinutes(1);
    }
}
