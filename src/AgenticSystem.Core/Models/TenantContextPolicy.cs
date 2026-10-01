using AgenticSystem.Core.Interfaces;

namespace AgenticSystem.Core.Models;

/// <summary>Validates that an operation is bound to the real tenant selected for its async flow.</summary>
public static class TenantContextPolicy
{
    public static string RequireCurrentTenant(ITenantContextAccessor accessor, string tenantId)
    {
        ArgumentNullException.ThrowIfNull(accessor);
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        var requestedTenantId = tenantId.Trim();
        if (TenantIdPolicy.IsReservedSystemId(requestedTenantId))
            throw new InvalidOperationException("A system operation identifier cannot be used as a tenant ID.");

        var currentTenantId = accessor.CurrentTenantId;
        if (!string.Equals(currentTenantId, requestedTenantId, StringComparison.Ordinal))
            throw new InvalidOperationException("The requested tenant does not match the active tenant context.");

        return currentTenantId;
    }
}
