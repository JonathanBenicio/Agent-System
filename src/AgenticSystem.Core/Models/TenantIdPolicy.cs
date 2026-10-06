namespace AgenticSystem.Core.Models;

/// <summary>Rules for rejecting legacy or synthetic identifiers that are reserved outside runtime tenant scope.</summary>
public static class TenantIdPolicy
{
    private static readonly HashSet<string> ReservedSystemIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "default",
        "platform",
        "system-background",
        "system-devui"
    };

    /// <summary>Returns true when the value is reserved for a legacy fixture or an internal system operation.</summary>
    public static bool IsReservedSystemId(string? tenantId) =>
        !string.IsNullOrWhiteSpace(tenantId) && ReservedSystemIds.Contains(tenantId.Trim());
}
