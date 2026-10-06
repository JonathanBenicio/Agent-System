using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AgenticSystem.Infrastructure.Persistence.Entities;
using AgenticSystem.Core.Interfaces;
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
    private readonly ITenantContextAccessor _tenantContextAccessor;
    private readonly ISystemOperationContextAccessor _systemOperations;
    private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(5);

    public OutboxProcessorBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<OutboxProcessorBackgroundService> logger,
        ITenantContextAccessor tenantContextAccessor,
        ISystemOperationContextAccessor systemOperations)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _tenantContextAccessor = tenantContextAccessor;
        _systemOperations = systemOperations;
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

        List<OutboxMessageEntity> messages;
        using (_systemOperations.BeginScope(Core.Models.SystemOperationKind.ProcessOutbox))
        {
            _systemOperations.Require(Core.Models.SystemOperationKind.ProcessOutbox);
            messages = await FilterDispatchableTenantMessages(dbContext.OutboxMessages
                .IgnoreQueryFilters()
                .Where(m => m.ProcessedAt == null && m.Error == null))
                .OrderBy(m => m.CreatedAt)
                .Take(50)
                .ToListAsync(stoppingToken);
        }

        foreach (var message in messages)
        {
            using var tenantScope = _tenantContextAccessor.BeginScope(new Core.Models.TenantContext { TenantId = message.TenantId });
            try
            {
                await DispatchAsync(message.EventType, message.PayloadJson, publisher, stoppingToken);
                message.ProcessedAt = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing OutboxMessage {MessageId}", message.Id);
                message.Error = ex.Message;
            }

            await dbContext.SaveChangesAsync(stoppingToken);
        }

        List<PlatformOutboxMessageEntity> platformMessages;
        using (_systemOperations.BeginScope(Core.Models.SystemOperationKind.ProcessPlatformOutbox))
        {
            _systemOperations.Require(Core.Models.SystemOperationKind.ProcessPlatformOutbox);
            platformMessages = await dbContext.PlatformOutboxMessages
                .Where(message => message.ProcessedAt == null && message.Error == null)
                .OrderBy(message => message.CreatedAt)
                .Take(50)
                .ToListAsync(stoppingToken);

            foreach (var message in platformMessages)
            {
                try
                {
                    await DispatchAsync(message.EventType, message.PayloadJson, publisher, stoppingToken);
                    message.ProcessedAt = DateTime.UtcNow;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing platform outbox message {MessageId}", message.Id);
                    message.Error = ex.Message;
                }

                await dbContext.SaveChangesAsync(stoppingToken);
            }
        }
    }

    internal static IQueryable<OutboxMessageEntity> FilterDispatchableTenantMessages(
        IQueryable<OutboxMessageEntity> messages) =>
        messages.Where(m => m.TenantId.Trim().ToLower() != "default" &&
                            m.TenantId.Trim().ToLower() != "platform" &&
                            m.TenantId.Trim().ToLower() != "system-background" &&
                            m.TenantId.Trim().ToLower() != "system-devui");

    private static async Task DispatchAsync(string eventTypeName, string payloadJson, IPublisher publisher, CancellationToken ct)
    {
        var eventType = Type.GetType(eventTypeName);
        if (eventType is null)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                eventType = assembly.GetType(eventTypeName);
                if (eventType is not null) break;
            }
        }

        if (eventType is null)
            throw new InvalidOperationException($"Outbox event type '{eventTypeName}' was not found.");

        var domainEvent = JsonSerializer.Deserialize(payloadJson, eventType)
            ?? throw new InvalidOperationException("Outbox payload deserialized to null.");
        if (domainEvent is INotification notification)
            await publisher.Publish(notification, ct);
        else
            await publisher.Publish(new Core.Models.DomainEventNotification(domainEvent), ct);
    }
}
