namespace AgenticSystem.Core.Models;

// ═══════════════════════════════════════════════════════════
// Agent Versioning — Snapshot de configuração versionada
// ═══════════════════════════════════════════════════════════

/// <summary>
/// Immutable snapshot of an agent's configuration at a specific version.
/// Enables rollback, staging/production promotion, and A/B testing.
/// </summary>
public class AgentVersion
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string AgentName { get; init; } = string.Empty;
    public int VersionNumber { get; init; }
    public string Label { get; init; } = string.Empty; // "v1.0", "v2.3-staging", etc.
    public AgentVersionStatus Status { get; set; } = AgentVersionStatus.Draft;
    public AgentVersionEnvironment Environment { get; set; } = AgentVersionEnvironment.Staging;

    // ─── Snapshot de configuração ───
    public string SystemPrompt { get; init; } = string.Empty;
    public string? ModelProvider { get; init; }
    public string? ModelId { get; init; }
    public List<string> Tools { get; init; } = [];
    public string? PolicySnapshotJson { get; init; }
    public Dictionary<string, object> Parameters { get; init; } = new();

    // ─── Metadados ───
    public string? Description { get; init; }
    public string? ChangeLog { get; init; }
    public string? CreatedBy { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime? PromotedAt { get; set; }
    public string? PromotedBy { get; set; }

    /// <summary>
    /// Hash of the configuration snapshot for integrity verification.
    /// </summary>
    public string? ConfigHash { get; init; }

    /// <summary>
    /// Reference to the parent version (null for the first version).
    /// </summary>
    public string? ParentVersionId { get; init; }
}

public enum AgentVersionStatus
{
    Draft,
    Active,
    Promoted,
    Deprecated,
    RolledBack
}

public enum AgentVersionEnvironment
{
    Staging,
    Production,
    Canary
}

/// <summary>
/// Result of a version promotion or rollback operation.
/// </summary>
public class AgentVersionOperationResult
{
    public bool Success { get; init; }
    public string? Message { get; init; }
    public AgentVersion? Version { get; init; }
    public AgentVersion? PreviousVersion { get; init; }
}
