using System.Data;
using AgenticSystem.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace AgenticSystem.Infrastructure.Persistence;

/// <summary>
/// Background service that listens for PostgreSQL NOTIFY events to trigger configuration reloads across all system nodes.
/// Implements the "Hot-Swapping Foundation" (Phase 0).
/// </summary>
public sealed class RealTimeConfigReloadBackgroundService : BackgroundService
{
    private const string ChannelName = "config_changed";
    private readonly IServiceProvider _serviceProvider;
    private readonly string _connectionString;
    private readonly ILogger<RealTimeConfigReloadBackgroundService> _logger;

    public RealTimeConfigReloadBackgroundService(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<RealTimeConfigReloadBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _connectionString = configuration.GetConnectionString("SessionStore") 
                            ?? throw new InvalidOperationException("ConnectionStrings:SessionStore is required for real-time config reloads.");
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("🚀 Starting Real-Time Config Reload Listener (Channel: {Channel})", ChannelName);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunListenerAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "❌ Error in Config Reload Listener. Retrying in 5 seconds...");
                await Task.Delay(5000, stoppingToken);
            }
        }
    }

    private async Task RunListenerAsync(CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        connection.Notification += (sender, e) =>
        {
            _logger.LogInformation("🔔 Received config reload notification for key: {Key}", e.Payload);
            
            using var scope = _serviceProvider.CreateScope();
            var notifier = scope.ServiceProvider.GetRequiredService<IConfigReloadNotifier>();
            notifier.NotifyChange(e.Payload);
        };

        await using (var command = new NpgsqlCommand($"LISTEN {ChannelName};", connection))
        {
            await command.ExecuteNonQueryAsync(ct);
        }

        _logger.LogInformation("📡 Listening for NOTIFY events on channel '{Channel}'...", ChannelName);

        while (!ct.IsCancellationRequested)
        {
            // Wait for notification or timeout to keep the connection alive
            await connection.WaitAsync(ct);
        }
    }
}
