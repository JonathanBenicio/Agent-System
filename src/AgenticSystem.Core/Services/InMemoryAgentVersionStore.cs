using System.Collections.Concurrent;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Services;

/// <summary>
/// In-memory implementation of IAgentVersionStore for development/testing.
/// </summary>
public class InMemoryAgentVersionStore : IAgentVersionStore
{
    private readonly ConcurrentDictionary<string, AgentVersion> _versions = new(StringComparer.OrdinalIgnoreCase);

    public Task SaveAsync(AgentVersion version, CancellationToken ct = default)
    {
        _versions[version.Id] = version;
        return Task.CompletedTask;
    }

    public Task<AgentVersion?> GetByIdAsync(string versionId, CancellationToken ct = default)
    {
        _versions.TryGetValue(versionId, out var version);
        return Task.FromResult(version);
    }

    public Task<AgentVersion?> GetActiveAsync(string agentName, AgentVersionEnvironment environment, CancellationToken ct = default)
    {
        var active = _versions.Values
            .Where(v => v.AgentName.Equals(agentName, StringComparison.OrdinalIgnoreCase))
            .Where(v => v.Environment == environment)
            .Where(v => v.Status == AgentVersionStatus.Promoted || v.Status == AgentVersionStatus.Active)
            .OrderByDescending(v => v.VersionNumber)
            .FirstOrDefault();

        return Task.FromResult(active);
    }

    public Task<IReadOnlyList<AgentVersion>> GetHistoryAsync(string agentName, int limit = 20, CancellationToken ct = default)
    {
        var history = _versions.Values
            .Where(v => v.AgentName.Equals(agentName, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(v => v.VersionNumber)
            .Take(limit)
            .ToList();

        return Task.FromResult<IReadOnlyList<AgentVersion>>(history);
    }

    public Task<int> GetNextVersionNumberAsync(string agentName, CancellationToken ct = default)
    {
        var max = _versions.Values
            .Where(v => v.AgentName.Equals(agentName, StringComparison.OrdinalIgnoreCase))
            .Select(v => v.VersionNumber)
            .DefaultIfEmpty(0)
            .Max();

        return Task.FromResult(max + 1);
    }
}
