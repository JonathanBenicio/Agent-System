#pragma warning disable MAAI001 // Required experimental MAF session-store integration; reviewed under issue #120.

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Infrastructure.AgentFramework;

/// <summary>Persists MAF session snapshots inside the owning application session record.</summary>
public sealed class SimpleSessionStoreAdapter : AgentSessionStore
{
    private readonly ISessionStore _sessionStore;
    private readonly ILogger<SimpleSessionStoreAdapter> _logger;
    private const string FrameworkStateKey = "frameworkSessionState";
    private const string IsolationPartition = "isolation";

    public SimpleSessionStoreAdapter(ISessionStore sessionStore, ILogger<SimpleSessionStoreAdapter> logger)
    {
        _sessionStore = sessionStore ?? throw new ArgumentNullException(nameof(sessionStore));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public override async ValueTask SaveSessionAsync(
        AIAgent agent,
        AgentSessionStoreKey key,
        AgentSession session,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(session);

        var sessionData = await GetOwnedSessionDataAsync(key, cancellationToken)
            ?? throw new InvalidOperationException($"Conversation '{key.SessionId}' does not exist in the session store.");
        var serialized = await agent.SerializeSessionAsync(session, cancellationToken: cancellationToken);
        var runtimeKey = BuildRuntimeKey(agent.Name, key);
        sessionData.RuntimeSettings[runtimeKey] = serialized.GetRawText();

        // The old hosting decorator prefixed a string ID and the adapter stripped it. Move a verified
        // legacy snapshot to the partition-aware key on the first successful write.
        if (CanReadLegacyState(key))
            sessionData.RuntimeSettings.Remove(BuildLegacyRuntimeKey(agent.Name));

        await _sessionStore.SaveAsync(sessionData, cancellationToken);
        _logger.LogDebug(
            "Framework session persisted: SessionId={SessionId}, Agent={AgentName}, Key={RuntimeKey}",
            key.SessionId,
            agent.Name,
            runtimeKey);
    }

    public override async ValueTask<AgentSession?> GetSessionAsync(
        AIAgent agent,
        AgentSessionStoreKey key,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(key);

        var sessionData = await GetOwnedSessionDataAsync(key, cancellationToken);
        if (sessionData is null)
            return null;

        var runtimeKey = BuildRuntimeKey(agent.Name, key);
        var hasScopedState = sessionData.RuntimeSettings.TryGetValue(runtimeKey, out var persistedState)
            && !string.IsNullOrWhiteSpace(persistedState);
        var migrateLegacyState = false;

        if (!hasScopedState && CanReadLegacyState(key)
            && sessionData.RuntimeSettings.TryGetValue(BuildLegacyRuntimeKey(agent.Name), out var legacyState)
            && !string.IsNullOrWhiteSpace(legacyState))
        {
            persistedState = legacyState;
            migrateLegacyState = true;
        }

        if (string.IsNullOrWhiteSpace(persistedState))
            return null;

        try
        {
            using var document = JsonDocument.Parse(persistedState);
            var restored = await agent.DeserializeSessionAsync(
                document.RootElement,
                cancellationToken: cancellationToken);

            if (migrateLegacyState)
            {
                sessionData.RuntimeSettings[runtimeKey] = persistedState;
                sessionData.RuntimeSettings.Remove(BuildLegacyRuntimeKey(agent.Name));
            }

            var restoredAtKey = runtimeKey.Replace(
                FrameworkStateKey,
                "frameworkSessionRestoredAt",
                StringComparison.Ordinal);
            sessionData.RuntimeSettings[restoredAtKey] = DateTime.UtcNow.ToString("O");
            await _sessionStore.SaveAsync(sessionData, cancellationToken);

            _logger.LogInformation(
                "Framework session restored from persisted MAF state: SessionId={SessionId}, Agent={AgentName}, LegacyMigrated={LegacyMigrated}",
                key.SessionId,
                agent.Name,
                migrateLegacyState);
            return restored;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to restore framework session. SessionId={SessionId}", key.SessionId);
            return null;
        }
    }

    private async Task<SessionData?> GetOwnedSessionDataAsync(AgentSessionStoreKey key, CancellationToken cancellationToken)
    {
        var sessionData = await _sessionStore.GetAsync(key.SessionId, cancellationToken);
        if (sessionData is null)
            return null;

        if (key.Partitions is not null
            && key.Partitions.TryGetValue(IsolationPartition, out var isolationKey)
            && !MatchesOwner(sessionData, isolationKey))
        {
            throw new InvalidOperationException("The MAF session is not owned by the authenticated tenant and user.");
        }

        return sessionData;
    }

    private static bool MatchesOwner(SessionData sessionData, string isolationKey)
    {
        var expectedKey = $"{sessionData.TenantId}:{sessionData.UserId}";
        return !string.IsNullOrWhiteSpace(sessionData.TenantId)
            && !string.IsNullOrWhiteSpace(sessionData.UserId)
            && string.Equals(expectedKey, isolationKey, StringComparison.OrdinalIgnoreCase);
    }

    private static bool CanReadLegacyState(AgentSessionStoreKey key) =>
        key.Partitions is { Count: 1 }
        && key.Partitions.ContainsKey(IsolationPartition);

    private static string BuildRuntimeKey(string? agentName, AgentSessionStoreKey key)
    {
        var baseKey = BuildLegacyRuntimeKey(agentName);
        if (key.Partitions is not { Count: > 0 })
            return baseKey;

        var canonicalPartitions = key.Partitions
            .OrderBy(partition => partition.Key, StringComparer.Ordinal)
            .Select(partition => new[] { partition.Key, partition.Value });
        var canonicalJson = JsonSerializer.Serialize(canonicalPartitions);
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalJson))).ToLowerInvariant();
        return $"{baseKey}:scope:{digest}";
    }

    private static string BuildLegacyRuntimeKey(string? agentName)
    {
        if (string.IsNullOrWhiteSpace(agentName))
            return $"{FrameworkStateKey}:default";

        return $"{FrameworkStateKey}:{agentName.Trim().ToLowerInvariant()}";
    }
}
