using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Interfaces;

/// <summary>
/// Internal event bus for publishing and subscribing to agent system events.
/// Supports tenant-scoped subscriptions, transactions, and dead-letter queue.
/// </summary>
public interface IEventBus
{
    // ─── Original contract (used by AgentRuntimeCoordinator) ───

    Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct = default) where TEvent : class;

    /// <summary>
    /// Executes a business operation and publishes events within the same ambient transaction.
    /// </summary>
    Task ExecuteInTransactionAsync(Func<Task> businessOperation, IEnumerable<object> events, CancellationToken ct = default);

    // ─── Enhanced event bus contract ───

    /// <summary>
    /// Publishes a typed system bus event to all matching subscribers.
    /// </summary>
    Task PublishAsync(SystemBusEvent busEvent, CancellationToken ct = default);

    /// <summary>
    /// Subscribes to events of a specific type.
    /// </summary>
    Task<EventSubscription> SubscribeAsync(
        string eventType,
        string subscriberName,
        Func<SystemBusEvent, Task> handler,
        string? tenantId = null,
        CancellationToken ct = default);

    /// <summary>
    /// Unsubscribes from events.
    /// </summary>
    Task UnsubscribeAsync(string subscriptionId, CancellationToken ct = default);

    /// <summary>
    /// Lists all active subscriptions.
    /// </summary>
    Task<IReadOnlyList<EventSubscription>> ListSubscriptionsAsync(
        string? eventType = null,
        CancellationToken ct = default);

    /// <summary>
    /// Returns dead-letter entries for review.
    /// </summary>
    Task<IReadOnlyList<DeadLetterEntry>> GetDeadLettersAsync(
        DeadLetterStatus? status = null,
        int limit = 50,
        CancellationToken ct = default);

    /// <summary>
    /// Retries a dead-letter entry.
    /// </summary>
    Task RetryDeadLetterAsync(string deadLetterId, CancellationToken ct = default);
}
