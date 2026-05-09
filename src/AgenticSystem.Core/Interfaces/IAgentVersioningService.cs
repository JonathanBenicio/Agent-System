using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Interfaces;

/// <summary>
/// Service for managing agent configuration versions.
/// Supports creating snapshots, promoting between environments, and rollback.
/// </summary>
public interface IAgentVersioningService
{
    /// <summary>
    /// Creates a new version snapshot from the current agent configuration.
    /// </summary>
    Task<AgentVersion> CreateVersionAsync(
        string agentName,
        string? description = null,
        string? changeLog = null,
        string? createdBy = null,
        CancellationToken ct = default);

    /// <summary>
    /// Returns the active (production) version for an agent.
    /// </summary>
    Task<AgentVersion?> GetActiveVersionAsync(
        string agentName,
        AgentVersionEnvironment environment = AgentVersionEnvironment.Production,
        CancellationToken ct = default);

    /// <summary>
    /// Returns the complete version history for an agent.
    /// </summary>
    Task<IReadOnlyList<AgentVersion>> GetVersionHistoryAsync(
        string agentName,
        int limit = 20,
        CancellationToken ct = default);

    /// <summary>
    /// Promotes a version from staging to production.
    /// The current production version is automatically deprecated.
    /// </summary>
    Task<AgentVersionOperationResult> PromoteAsync(
        string versionId,
        string promotedBy,
        CancellationToken ct = default);

    /// <summary>
    /// Rolls back to a specific previous version.
    /// Creates a new version that is a copy of the target version.
    /// </summary>
    Task<AgentVersionOperationResult> RollbackAsync(
        string agentName,
        string targetVersionId,
        string rolledBackBy,
        CancellationToken ct = default);

    /// <summary>
    /// Compares two versions and returns the differences.
    /// </summary>
    Task<AgentVersionDiff> DiffAsync(
        string versionIdA,
        string versionIdB,
        CancellationToken ct = default);
}

/// <summary>
/// Persistence store for agent versions.
/// </summary>
public interface IAgentVersionStore
{
    Task SaveAsync(AgentVersion version, CancellationToken ct = default);
    Task<AgentVersion?> GetByIdAsync(string versionId, CancellationToken ct = default);
    Task<AgentVersion?> GetActiveAsync(string agentName, AgentVersionEnvironment environment, CancellationToken ct = default);
    Task<IReadOnlyList<AgentVersion>> GetHistoryAsync(string agentName, int limit = 20, CancellationToken ct = default);
    Task<int> GetNextVersionNumberAsync(string agentName, CancellationToken ct = default);
}

/// <summary>
/// Diff result between two agent versions.
/// </summary>
public class AgentVersionDiff
{
    public string VersionIdA { get; init; } = string.Empty;
    public string VersionIdB { get; init; } = string.Empty;
    public bool HasPromptChanges { get; init; }
    public bool HasToolChanges { get; init; }
    public bool HasModelChanges { get; init; }
    public bool HasPolicyChanges { get; init; }
    public bool HasParameterChanges { get; init; }
    public List<string> AddedTools { get; init; } = [];
    public List<string> RemovedTools { get; init; } = [];
    public string? PromptDiffSummary { get; init; }
}
