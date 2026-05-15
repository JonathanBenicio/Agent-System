using MediatR;

namespace AgenticSystem.Core.Models;

/// <summary>
/// A wrapper for domain events that are not natively INotification.
/// </summary>
public record DomainEventNotification(object DomainEvent) : INotification;
