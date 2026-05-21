namespace AgenticSystem.Core.Models;

public record SessionCreatedEvent(string SessionId, string UserId, string TenantId);
public record SessionEndedEvent(string SessionId, string UserId, string TenantId);
