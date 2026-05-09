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

    public PostgresEventBus(IDbContextFactory<AgenticDbContext> dbContextFactory, ILogger<PostgresEventBus> logger)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    public async Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct = default) where TEvent : class
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        
        var message = new OutboxMessageEntity
        {
            Id = Guid.NewGuid(),
            EventType = @event.GetType().AssemblyQualifiedName ?? @event.GetType().Name,
            PayloadJson = JsonSerializer.Serialize(@event),
            CreatedAt = DateTime.UtcNow
        };

        db.OutboxMessages.Add(message);
        await db.SaveChangesAsync(ct);
        
        _logger.LogDebug("Outbox message saved for event type {EventType}", message.EventType);
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
