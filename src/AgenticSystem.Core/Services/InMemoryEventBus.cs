using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using System.Collections.Concurrent;
using MediatR;

namespace AgenticSystem.Core.Services;

/// <summary>
/// A simple in-memory implementation of the IEventBus.
/// Suitable for local development and testing without external message brokers or persistent storage.
/// </summary>
public class InMemoryEventBus : IEventBus
{
    private readonly IPublisher _publisher;
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, Func<SystemBusEvent, Task>>> _handlers = new();
    private readonly ConcurrentDictionary<string, EventSubscription> _subscriptions = new();
    private readonly List<DeadLetterEntry> _deadLetters = new();

    public InMemoryEventBus(IPublisher publisher)
    {
        _publisher = publisher;
    }

    public async Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct = default) where TEvent : class
    {
        if (@event is SystemBusEvent busEvent)
        {
            await PublishAsync(busEvent, ct);
            return;
        }

        // Publish to MediatR to ensure listeners (like Handlers) get the event
        if (@event is INotification notification)
        {
            await _publisher.Publish(notification, ct);
        }
        else
        {
            await _publisher.Publish(new DomainEventNotification(@event), ct);
        }
    }

    public async Task ExecuteInTransactionAsync(Func<Task> businessOperation, IEnumerable<object> events, CancellationToken ct = default)
    {
        // In-memory doesn't have true transactions, but we execute the operation then publish events
        await businessOperation();
        
        foreach (var @event in events)
        {
            if (@event is SystemBusEvent busEvent)
            {
                await PublishAsync(busEvent, ct);
            }
            else if (@event is INotification notification)
            {
                await _publisher.Publish(notification, ct);
            }
            else
            {
                await _publisher.Publish(new DomainEventNotification(@event), ct);
            }
        }
    }

    public async Task PublishAsync(SystemBusEvent busEvent, CancellationToken ct = default)
    {
        // 1. Internal handlers
        if (_handlers.TryGetValue(busEvent.EventType, out var typeHandlers))
        {
            foreach (var handler in typeHandlers.Values)
            {
                try
                {
                    await handler(busEvent);
                }
                catch (Exception ex)
                {
                    lock (_deadLetters)
                    {
                        _deadLetters.Add(new DeadLetterEntry
                        {
                            Id = Guid.NewGuid().ToString(),
                            Event = busEvent,
                            FailedAt = DateTime.UtcNow,
                            ErrorMessage = ex.Message,
                            Status = DeadLetterStatus.Pending
                        });
                    }
                }
            }
        }

        // 2. MediatR publishing (for subscribers using INotification)
        await _publisher.Publish(busEvent, ct);
    }

    public Task<EventSubscription> SubscribeAsync(
        string eventType,
        string subscriberName,
        Func<SystemBusEvent, Task> handler,
        string? tenantId = null,
        CancellationToken ct = default)
    {
        var subscriptionId = Guid.NewGuid().ToString();
        var subscription = new EventSubscription
        {
            Id = subscriptionId,
            EventType = eventType,
            SubscriberName = subscriberName,
            TenantId = tenantId,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        var typeHandlers = _handlers.GetOrAdd(eventType, _ => new ConcurrentDictionary<string, Func<SystemBusEvent, Task>>());
        typeHandlers[subscriptionId] = handler;
        _subscriptions[subscriptionId] = subscription;

        return Task.FromResult(subscription);
    }

    public Task UnsubscribeAsync(string subscriptionId, CancellationToken ct = default)
    {
        if (_subscriptions.TryRemove(subscriptionId, out var sub))
        {
            if (_handlers.TryGetValue(sub.EventType, out var typeHandlers))
            {
                typeHandlers.TryRemove(subscriptionId, out _);
            }
        }
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<EventSubscription>> ListSubscriptionsAsync(string? eventType = null, CancellationToken ct = default)
    {
        IEnumerable<EventSubscription> query = _subscriptions.Values;
        if (!string.IsNullOrEmpty(eventType))
        {
            query = query.Where(s => s.EventType == eventType);
        }
        return Task.FromResult<IReadOnlyList<EventSubscription>>(query.ToList());
    }

    public Task<IReadOnlyList<DeadLetterEntry>> GetDeadLettersAsync(DeadLetterStatus? status = null, int limit = 50, CancellationToken ct = default)
    {
        lock (_deadLetters)
        {
            IEnumerable<DeadLetterEntry> query = _deadLetters;
            if (status.HasValue)
            {
                query = query.Where(d => d.Status == status.Value);
            }
            return Task.FromResult<IReadOnlyList<DeadLetterEntry>>(query.Take(limit).ToList());
        }
    }

    public Task RetryDeadLetterAsync(string deadLetterId, CancellationToken ct = default)
    {
        // Not implemented for in-memory
        return Task.CompletedTask;
    }
}
