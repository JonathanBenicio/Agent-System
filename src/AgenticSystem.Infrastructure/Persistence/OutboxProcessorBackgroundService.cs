using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AgenticSystem.Infrastructure.Persistence.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Infrastructure.Persistence;

public class OutboxProcessorBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OutboxProcessorBackgroundService> _logger;
    private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(5);

    public OutboxProcessorBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<OutboxProcessorBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OutboxProcessorBackgroundService is starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessOutboxMessagesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while processing outbox messages.");
            }

            await Task.Delay(_pollInterval, stoppingToken);
        }

        _logger.LogInformation("OutboxProcessorBackgroundService is stopping.");
    }

    private async Task ProcessOutboxMessagesAsync(CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AgenticDbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();

        var messages = await dbContext.OutboxMessages
            .Where(m => m.ProcessedAt == null && m.Error == null)
            .OrderBy(m => m.CreatedAt)
            .Take(50)
            .ToListAsync(stoppingToken);

        if (!messages.Any())
        {
            return;
        }

        foreach (var message in messages)
        {
            try
            {
                var eventType = Type.GetType(message.EventType);
                if (eventType == null)
                {
                    // Fallback to checking loaded assemblies if Type.GetType fails for types not in mscorlib/current assembly
                    foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        eventType = assembly.GetType(message.EventType);
                        if (eventType != null)
                            break;
                    }
                }

                if (eventType == null)
                {
                    message.Error = $"Type {message.EventType} not found.";
                    _logger.LogWarning("Type {EventType} not found for OutboxMessage {MessageId}", message.EventType, message.Id);
                    continue;
                }

                var domainEvent = JsonSerializer.Deserialize(message.PayloadJson, eventType);
                if (domainEvent == null)
                {
                    message.Error = $"Deserialization returned null for payload.";
                    continue;
                }

                await publisher.Publish(domainEvent, stoppingToken);

                message.ProcessedAt = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing OutboxMessage {MessageId}", message.Id);
                message.Error = ex.Message;
            }
        }

        await dbContext.SaveChangesAsync(stoppingToken);
    }
}
