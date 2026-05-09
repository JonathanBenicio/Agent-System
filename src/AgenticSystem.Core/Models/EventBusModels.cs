namespace AgenticSystem.Core.Models;

// ═══════════════════════════════════════════════════════════
// Event Bus — Internal Event System with Dead-Letter
// ═══════════════════════════════════════════════════════════

/// <summary>
/// An event published through the internal event bus.
/// </summary>
public class SystemBusEvent
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string EventType { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
    public string? TenantId { get; init; }
    public Dictionary<string, object> Payload { get; init; } = new();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public string? CorrelationId { get; init; }
}

/// <summary>
/// An event subscription.
/// </summary>
public class EventSubscription
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string EventType { get; init; } = string.Empty;
    public string SubscriberName { get; init; } = string.Empty;
    public string? TenantId { get; init; }
    public string? FilterExpression { get; init; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// A failed event delivery stored in the dead-letter queue.
/// </summary>
public class DeadLetterEntry
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public SystemBusEvent Event { get; init; } = new();
    public string SubscriptionId { get; init; } = string.Empty;
    public string ErrorMessage { get; init; } = string.Empty;
    public int RetryCount { get; init; }
    public DateTime FailedAt { get; init; } = DateTime.UtcNow;
    public DeadLetterStatus Status { get; set; } = DeadLetterStatus.Pending;
}

public enum DeadLetterStatus
{
    Pending,
    Retrying,
    Resolved,
    Discarded
}
