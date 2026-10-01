namespace AgenticSystem.Core.Models;

public sealed record ChatSettings(string TenantId, string UserId, string Provider, string Model, DateTime UpdatedAt);
