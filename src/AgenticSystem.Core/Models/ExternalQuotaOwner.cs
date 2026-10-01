namespace AgenticSystem.Core.Models;

/// <summary>
/// Identifies whether an external provider quota belongs to one real tenant or to platform infrastructure.
/// A platform quota is represented by this typed scope, never by a synthetic TenantId.
/// </summary>
public sealed record ExternalQuotaOwner
{
    private ExternalQuotaOwner(string? tenantId) => TenantId = tenantId;

    /// <summary>The real tenant ID, or null for platform infrastructure quotas.</summary>
    public string? TenantId { get; }

    /// <summary>True when the quota belongs to platform infrastructure.</summary>
    public bool IsPlatform => TenantId is null;

    /// <summary>Platform-wide quota scope for infrastructure keys.</summary>
    public static ExternalQuotaOwner Platform { get; } = new((string?)null);

    /// <summary>Creates a quota owner for a real tenant.</summary>
    public static ExternalQuotaOwner ForTenant(string tenantId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        if (TenantIdPolicy.IsReservedSystemId(tenantId))
            throw new ArgumentException("System operation identifiers are not tenant quota owners.", nameof(tenantId));
        return new ExternalQuotaOwner(tenantId.Trim());
    }
}
