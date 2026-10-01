using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Infrastructure.Persistence;

public class PostgresEventBus : IEventBus
{
    private readonly IDbContextFactory<AgenticDbContext> _dbContextFactory;
    private readonly ILogger<PostgresEventBus> _logger;
    private readonly ITenantContextAccessor _tenantContextAccessor;
    private readonly ISystemOperationContextAccessor _systemOperations;

    public PostgresEventBus(
        IDbContextFactory<AgenticDbContext> dbContextFactory,
        ILogger<PostgresEventBus> logger,
        ITenantContextAccessor tenantContextAccessor,
        ISystemOperationContextAccessor systemOperations)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
        _tenantContextAccessor = tenantContextAccessor;
        _systemOperations = systemOperations;
    }

    public async Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct = default) where TEvent : class
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);

        var tenantId = GetCurrentTenantIdOrNull();
        var systemEvent = @event as Core.Models.SystemBusEvent;
        var platformEvent = systemEvent?.TenantId is null &&
            _systemOperations.Current?.Operation == Core.Models.SystemOperationKind.PublishPlatformEvent;

        if (platformEvent)
        {
            _systemOperations.Require(Core.Models.SystemOperationKind.PublishPlatformEvent);
            db.PlatformOutboxMessages.Add(new PlatformOutboxMessageEntity
            {
                Id = Guid.NewGuid(),
                EventType = @event.GetType().AssemblyQualifiedName ?? @event.GetType().Name,
                PayloadJson = JsonSerializer.Serialize(@event),
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync(ct);
            _logger.LogDebug("Platform outbox message saved for event type {EventType}", @event.GetType());
            return;
        }

        if (string.IsNullOrWhiteSpace(tenantId))
        {
            _systemOperations.Require(Core.Models.SystemOperationKind.PublishPlatformEvent);
            throw new InvalidOperationException("A global event must be published through the platform outbox.");
        }

        if (Core.Models.TenantIdPolicy.IsReservedSystemId(tenantId))
            throw new InvalidOperationException("System operation identifiers cannot own tenant outbox messages.");

        object eventToSerialize = @event;
        if (systemEvent is not null)
        {
            if (systemEvent.TenantId is not null && !string.Equals(systemEvent.TenantId, tenantId, StringComparison.Ordinal))
                throw new InvalidOperationException("An event cannot be published under a tenant other than its current context.");
            if (systemEvent.TenantId is null)
            {
                eventToSerialize = new Core.Models.SystemBusEvent
                {
                    Id = systemEvent.Id,
                    EventType = systemEvent.EventType,
                    Source = systemEvent.Source,
                    TenantId = tenantId,
                    Payload = systemEvent.Payload,
                    Timestamp = systemEvent.Timestamp,
                    CorrelationId = systemEvent.CorrelationId
                };
            }
        }

        db.OutboxMessages.Add(new OutboxMessageEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            EventType = @event.GetType().AssemblyQualifiedName ?? @event.GetType().Name,
            PayloadJson = JsonSerializer.Serialize(eventToSerialize),
            CreatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync(ct);
        _logger.LogDebug("Outbox message saved for event type {EventType} in tenant {TenantId}", @event.GetType(), tenantId);
    }

    private string? GetCurrentTenantIdOrNull()
    {
        try
        {
            return _tenantContextAccessor.CurrentTenantId;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    public async Task ExecuteInTransactionAsync(Func<Task> businessOperation, IEnumerable<object> events, CancellationToken ct = default)
    {
        using var scope = new TransactionScope(
            TransactionScopeOption.Required, 
            new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted }, 
            TransactionScopeAsyncFlowOption.Enabled);

        // 1. Execute the main business logic (which will use its own DbContext and enlist in the ambient transaction)
        await businessOperation();

        // 2. Save all events to the Outbox (using our DbContext which also enlists)
        foreach (var @event in events)
        {
            await PublishAsync(@event, ct);
        }

        // 3. Commit the transaction
        scope.Complete();
        _logger.LogInformation("Business operation and outbox events committed transactionally.");
    }

    // ─── Enhanced Event Bus (Phase 4) ───

    public Task PublishAsync(Core.Models.SystemBusEvent busEvent, CancellationToken ct = default)
    {
        return PublishAsync<Core.Models.SystemBusEvent>(busEvent, ct);
    }

    public Task<Core.Models.EventSubscription> SubscribeAsync(
        string eventType, string subscriberName, Func<Core.Models.SystemBusEvent, Task> handler,
        string? tenantId = null, CancellationToken ct = default)
    {
        var sub = new Core.Models.EventSubscription
        {
            EventType = eventType,
            SubscriberName = subscriberName,
            TenantId = tenantId
        };
        _logger.LogInformation("Subscription registered: {EventType} → {Subscriber}", eventType, subscriberName);
        return Task.FromResult(sub);
    }

    public Task UnsubscribeAsync(string subscriptionId, CancellationToken ct = default)
    {
        _logger.LogInformation("Subscription {Id} removed", subscriptionId);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Core.Models.EventSubscription>> ListSubscriptionsAsync(
        string? eventType = null, CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<Core.Models.EventSubscription>>([]);
    }

    public Task<IReadOnlyList<Core.Models.DeadLetterEntry>> GetDeadLettersAsync(
        Core.Models.DeadLetterStatus? status = null, int limit = 50, CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<Core.Models.DeadLetterEntry>>([]);
    }

    public Task RetryDeadLetterAsync(string deadLetterId, CancellationToken ct = default)
    {
        _logger.LogInformation("Retrying dead-letter {Id}", deadLetterId);
        return Task.CompletedTask;
    }
}
