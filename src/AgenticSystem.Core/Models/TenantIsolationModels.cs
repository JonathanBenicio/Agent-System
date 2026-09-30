namespace AgenticSystem.Core.Models;

// ═══════════════════════════════════════════════════════════
// Tenant Isolation — Enhanced Multi-Tenancy
// ═══════════════════════════════════════════════════════════

/// <summary>
/// Enhanced tenant configuration with resource isolation settings.
/// </summary>
public class TenantIsolationConfig
{
    public string TenantId { get; init; } = string.Empty;
    public TenantIsolationLevel IsolationLevel { get; init; } = TenantIsolationLevel.Shared;
    public TenantLimits Limits { get; init; } = TenantLimits.FreeTier();
    public TenantResourceLimits ResourceLimits => TenantResourceLimits.From(Limits);
    public TenantStorageConfig Storage { get; init; } = new();
    public List<string> AllowedRegions { get; init; } = [];
    public bool DataEncryptionAtRest { get; init; } = true;
    public string? DedicatedApiKeyPrefix { get; init; }
}

public enum TenantIsolationLevel
{
    Shared,      // Shared infrastructure, logical separation
    Dedicated,   // Dedicated compute/storage resources
    Isolated     // Fully isolated runtime (separate containers)
}

/// <summary>
/// Resource limits per tenant.
/// </summary>
public class TenantResourceLimits
{
    public int MaxConcurrentSessions { get; init; }
    public int MaxStorageMb { get; init; }
    public int MaxDocuments { get; init; }
    public int MaxAgents { get; init; }
    public double MaxMonthlyBudgetUsd { get; init; }

    public static TenantResourceLimits From(TenantLimits limits) => new()
    {
        MaxConcurrentSessions = limits.MaxConcurrentSessions,
        MaxStorageMb = limits.MaxDocumentsMb,
        MaxDocuments = limits.MaxDocuments,
        MaxAgents = limits.MaxAgents,
        MaxMonthlyBudgetUsd = (double)(limits.MaxDailyCostUsd * 30m)
    };
}

/// <summary>
/// Tenant-specific storage configuration.
/// </summary>
public class TenantStorageConfig
{
    public string? VectorDbNamespace { get; init; }
    public string? BlobContainerPrefix { get; init; }
    public string? CacheKeyPrefix { get; init; }
    public string? QueuePrefix { get; init; }
}
