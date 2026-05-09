namespace AgenticSystem.Core.Models;

// ═══════════════════════════════════════════════════════════
// Data Connectors — External Source Integration
// ═══════════════════════════════════════════════════════════

/// <summary>
/// Configuration for an external data connector.
/// </summary>
public class DataConnectorConfig
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Name { get; init; } = string.Empty;
    public DataConnectorType ConnectorType { get; init; }
    public string ConnectionString { get; init; } = string.Empty;
    public Dictionary<string, string> Settings { get; init; } = new();
    public string? TenantId { get; init; }
    public DataSyncSchedule SyncSchedule { get; init; } = new();
    public bool IsActive { get; set; } = true;
    public DateTime? LastSyncAt { get; set; }
    public ConnectorStatus Status { get; set; } = ConnectorStatus.Configured;
}

public enum DataConnectorType
{
    Obsidian,
    SharePoint,
    GoogleDrive,
    Notion,
    Confluence,
    Jira,
    GitHub,
    SqlDatabase,
    RestApi,
    S3Bucket,
    FileSystem
}

public enum ConnectorStatus
{
    Configured,
    Syncing,
    Ready,
    Error,
    Disabled
}

/// <summary>
/// Schedule for data synchronization.
/// </summary>
public class DataSyncSchedule
{
    public bool AutoSync { get; init; } = false;
    public string? CronExpression { get; init; }
    public TimeSpan? SyncInterval { get; init; }
    public bool IncrementalSync { get; init; } = true;
}

/// <summary>
/// Result of a data sync operation.
/// </summary>
public class DataSyncResult
{
    public string ConnectorId { get; init; } = string.Empty;
    public bool Success { get; init; }
    public int DocumentsSynced { get; init; }
    public int DocumentsUpdated { get; init; }
    public int DocumentsDeleted { get; init; }
    public int Errors { get; init; }
    public TimeSpan Duration { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTime SyncedAt { get; init; } = DateTime.UtcNow;
}
