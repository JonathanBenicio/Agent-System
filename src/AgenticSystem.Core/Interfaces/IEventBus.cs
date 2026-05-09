using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AgenticSystem.Core.Interfaces;

public interface IEventBus
{
    Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct = default) where TEvent : class;
    
    /// <summary>
    /// Executes a business operation and publishes events within the same ambient transaction.
    /// </summary>
    Task ExecuteInTransactionAsync(Func<Task> businessOperation, IEnumerable<object> events, CancellationToken ct = default);
}
