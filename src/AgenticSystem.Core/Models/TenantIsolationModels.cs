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
    public TenantResourceLimits ResourceLimits { get; init; } = new();
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
    public int MaxConcurrentSessions { get; init; } = 10;
    public int MaxStorageMb { get; init; } = 1000;
    public int MaxDocuments { get; init; } = 10000;
    public int MaxAgents { get; init; } = 20;
    public double MaxMonthlyBudgetUsd { get; init; } = 100;
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
