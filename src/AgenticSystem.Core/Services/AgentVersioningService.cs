using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Core.Services;

/// <summary>
/// Manages agent configuration versioning with promotion and rollback support.
/// </summary>
public class AgentVersioningService : IAgentVersioningService
{
    private readonly IAgentVersionStore _store;
    private readonly IAuditLog _auditLog;
    private readonly ILogger<AgentVersioningService> _logger;
    private readonly Func<string, IAgent?> _agentResolver;

    public AgentVersioningService(
        IAgentVersionStore store,
        IAuditLog auditLog,
        Func<string, IAgent?> agentResolver,
        ILogger<AgentVersioningService> logger)
    {
        _store = store;
        _auditLog = auditLog;
        _agentResolver = agentResolver;
        _logger = logger;
    }

    public async Task<AgentVersion> CreateVersionAsync(
        string agentName,
        string? description = null,
        string? changeLog = null,
        string? createdBy = null,
        CancellationToken ct = default)
    {
        var agent = _agentResolver(agentName)
            ?? throw new ArgumentException($"Agent '{agentName}' not found.");

        var versionNumber = await _store.GetNextVersionNumberAsync(agentName, ct);
        var currentActive = await _store.GetActiveAsync(agentName, AgentVersionEnvironment.Production, ct);

        var snapshot = new AgentVersion
        {
            AgentName = agentName,
            VersionNumber = versionNumber,
            Label = $"v{versionNumber}.0",
            Status = AgentVersionStatus.Draft,
            Environment = AgentVersionEnvironment.Staging,
            SystemPrompt = agent.Instructions,
            Tools = agent.AvailableTools.ToList(),
            Description = description,
            ChangeLog = changeLog,
            CreatedBy = createdBy,
            ParentVersionId = currentActive?.Id,
            ConfigHash = ComputeHash(agent.Instructions, agent.AvailableTools)
        };

        await _store.SaveAsync(snapshot, ct);

        await _auditLog.RecordAsync(new AuditEntry
        {
            Category = AuditCategory.ConfigChange,
            Action = "AgentVersion.Created",
            AgentName = agentName,
            Description = $"Version {snapshot.Label} created for agent '{agentName}'.",
            Metadata = new Dictionary<string, object>
            {
                ["versionId"] = snapshot.Id,
                ["versionNumber"] = versionNumber,
                ["configHash"] = snapshot.ConfigHash ?? "N/A"
            }
        }, ct);

        _logger.LogInformation("Created version {Label} for agent {Agent}", snapshot.Label, agentName);
        return snapshot;
    }

    public async Task<AgentVersion?> GetActiveVersionAsync(
        string agentName,
        AgentVersionEnvironment environment = AgentVersionEnvironment.Production,
        CancellationToken ct = default)
    {
        return await _store.GetActiveAsync(agentName, environment, ct);
    }

    public async Task<IReadOnlyList<AgentVersion>> GetVersionHistoryAsync(
        string agentName,
        int limit = 20,
        CancellationToken ct = default)
    {
        return await _store.GetHistoryAsync(agentName, limit, ct);
    }

    public async Task<AgentVersionOperationResult> PromoteAsync(
        string versionId,
        string promotedBy,
        CancellationToken ct = default)
    {
        var version = await _store.GetByIdAsync(versionId, ct);
        if (version == null)
        {
            return new AgentVersionOperationResult
            {
                Success = false,
                Message = $"Version '{versionId}' not found."
            };
        }

        if (version.Status == AgentVersionStatus.Promoted)
        {
            return new AgentVersionOperationResult
            {
                Success = false,
                Message = "Version is already promoted to production.",
                Version = version
            };
        }

        // Deprecate current production version
        var currentProd = await _store.GetActiveAsync(version.AgentName, AgentVersionEnvironment.Production, ct);
        if (currentProd != null)
        {
            currentProd.Status = AgentVersionStatus.Deprecated;
            await _store.SaveAsync(currentProd, ct);
        }

        // Promote the new version
        version.Status = AgentVersionStatus.Promoted;
        version.Environment = AgentVersionEnvironment.Production;
        version.PromotedAt = DateTime.UtcNow;
        version.PromotedBy = promotedBy;
        await _store.SaveAsync(version, ct);

        await _auditLog.RecordAsync(new AuditEntry
        {
            Category = AuditCategory.ConfigChange,
            Action = "AgentVersion.Promoted",
            AgentName = version.AgentName,
            Description = $"Version {version.Label} promoted to production by {promotedBy}.",
            Metadata = new Dictionary<string, object>
            {
                ["versionId"] = version.Id,
                ["previousVersionId"] = currentProd?.Id ?? "none",
                ["promotedBy"] = promotedBy
            }
        }, ct);

        _logger.LogInformation("Promoted version {Label} for agent {Agent} to production",
            version.Label, version.AgentName);

        return new AgentVersionOperationResult
        {
            Success = true,
            Message = $"Version {version.Label} promoted to production.",
            Version = version,
            PreviousVersion = currentProd
        };
    }

    public async Task<AgentVersionOperationResult> RollbackAsync(
        string agentName,
        string targetVersionId,
        string rolledBackBy,
        CancellationToken ct = default)
    {
        var targetVersion = await _store.GetByIdAsync(targetVersionId, ct);
        if (targetVersion == null || targetVersion.AgentName != agentName)
        {
            return new AgentVersionOperationResult
            {
                Success = false,
                Message = $"Target version '{targetVersionId}' not found for agent '{agentName}'."
            };
        }

        // Deprecate current production version
        var currentProd = await _store.GetActiveAsync(agentName, AgentVersionEnvironment.Production, ct);
        if (currentProd != null)
        {
            currentProd.Status = AgentVersionStatus.RolledBack;
            await _store.SaveAsync(currentProd, ct);
        }

        // Create a new version as copy of target
        var versionNumber = await _store.GetNextVersionNumberAsync(agentName, ct);
        var rollbackVersion = new AgentVersion
        {
            AgentName = agentName,
            VersionNumber = versionNumber,
            Label = $"v{versionNumber}.0-rollback",
            Status = AgentVersionStatus.Promoted,
            Environment = AgentVersionEnvironment.Production,
            SystemPrompt = targetVersion.SystemPrompt,
            ModelProvider = targetVersion.ModelProvider,
            ModelId = targetVersion.ModelId,
            Tools = [.. targetVersion.Tools],
            PolicySnapshotJson = targetVersion.PolicySnapshotJson,
            Parameters = new Dictionary<string, object>(targetVersion.Parameters),
            Description = $"Rollback to {targetVersion.Label}",
            ChangeLog = $"Rolled back from {currentProd?.Label ?? "unknown"} to {targetVersion.Label}",
            CreatedBy = rolledBackBy,
            ParentVersionId = targetVersion.Id,
            PromotedAt = DateTime.UtcNow,
            PromotedBy = rolledBackBy,
            ConfigHash = targetVersion.ConfigHash
        };

        await _store.SaveAsync(rollbackVersion, ct);

        await _auditLog.RecordAsync(new AuditEntry
        {
            Category = AuditCategory.ConfigChange,
            Action = "AgentVersion.Rollback",
            AgentName = agentName,
            Description = $"Agent '{agentName}' rolled back to version {targetVersion.Label} by {rolledBackBy}.",
            Metadata = new Dictionary<string, object>
            {
                ["rollbackVersionId"] = rollbackVersion.Id,
                ["targetVersionId"] = targetVersionId,
                ["previousVersionId"] = currentProd?.Id ?? "none",
                ["rolledBackBy"] = rolledBackBy
            }
        }, ct);

        _logger.LogWarning("Rolled back agent {Agent} to version {TargetLabel}", agentName, targetVersion.Label);

        return new AgentVersionOperationResult
        {
            Success = true,
            Message = $"Rolled back to version {targetVersion.Label}.",
            Version = rollbackVersion,
            PreviousVersion = currentProd
        };
    }

    public async Task<AgentVersionDiff> DiffAsync(
        string versionIdA,
        string versionIdB,
        CancellationToken ct = default)
    {
        var a = await _store.GetByIdAsync(versionIdA, ct)
            ?? throw new ArgumentException($"Version '{versionIdA}' not found.");
        var b = await _store.GetByIdAsync(versionIdB, ct)
            ?? throw new ArgumentException($"Version '{versionIdB}' not found.");

        var toolsA = new HashSet<string>(a.Tools, StringComparer.OrdinalIgnoreCase);
        var toolsB = new HashSet<string>(b.Tools, StringComparer.OrdinalIgnoreCase);

        return new AgentVersionDiff
        {
            VersionIdA = versionIdA,
            VersionIdB = versionIdB,
            HasPromptChanges = a.SystemPrompt != b.SystemPrompt,
            HasToolChanges = !toolsA.SetEquals(toolsB),
            HasModelChanges = a.ModelProvider != b.ModelProvider || a.ModelId != b.ModelId,
            HasPolicyChanges = a.PolicySnapshotJson != b.PolicySnapshotJson,
            HasParameterChanges = JsonSerializer.Serialize(a.Parameters) != JsonSerializer.Serialize(b.Parameters),
            AddedTools = toolsB.Except(toolsA).ToList(),
            RemovedTools = toolsA.Except(toolsB).ToList(),
            PromptDiffSummary = a.SystemPrompt != b.SystemPrompt
                ? $"Prompt changed: {a.SystemPrompt.Length} → {b.SystemPrompt.Length} chars"
                : null
        };
    }

    private static string ComputeHash(string prompt, IEnumerable<string> tools)
    {
        var payload = $"{prompt}|{string.Join(",", tools.OrderBy(t => t))}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexStringLower(bytes)[..16];
    }
}
