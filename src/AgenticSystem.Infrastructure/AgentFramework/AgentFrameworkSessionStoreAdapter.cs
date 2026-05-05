using System.Text.Json;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Infrastructure.AgentFramework;

/// <summary>
/// Adapter do store de sessão da aplicação para o contract de AgentSessionStore do hosting nativo.
/// Persiste estados do framework na sessão de negócio usando uma chave estável por nome do agent,
/// com fallback legado por agent id e por eventos históricos.
/// </summary>
public sealed class AgentFrameworkSessionStoreAdapter : AgentSessionStore
{
    private readonly ISessionManager _sessionManager;
    private readonly ISessionStore? _sessionStore;
    private readonly ILogger<AgentFrameworkSessionStoreAdapter> _logger;

    public AgentFrameworkSessionStoreAdapter(
        ISessionManager sessionManager,
        ILogger<AgentFrameworkSessionStoreAdapter> logger,
        ISessionStore? sessionStore = null)
    {
        _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _sessionStore = sessionStore;
    }

    public override async ValueTask SaveSessionAsync(
        AIAgent agent,
        string conversationId,
        AgentSession session,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);
        ArgumentNullException.ThrowIfNull(session);

        try
        {
            var stableAgentName = FrameworkSessionStateKeyResolver.ResolveAgentName(agent.Name, agent.Id);
            var serialized = await agent.SerializeSessionAsync(session, cancellationToken: cancellationToken);
            var stateJson = serialized.GetRawText();

            if (_sessionStore != null)
            {
                var sessionData = await _sessionStore.GetAsync(conversationId, cancellationToken);
                if (sessionData != null)
                {
                    sessionData.RuntimeSettings[FrameworkSessionStateKeyResolver.BuildRuntimeKey(stableAgentName)] = stateJson;
                    await _sessionStore.SaveAsync(sessionData, cancellationToken);
                    _logger.LogDebug(
                        "Framework session state persisted via AgentSessionStore adapter: {ConversationId}, AgentName={AgentName}",
                        conversationId,
                        stableAgentName);
                    return;
                }
            }

            var stateEvent = new AgentEvent
            {
                SessionId = conversationId,
                AgentName = "AgentFramework.Session",
                UserInput = "[framework-session-state]",
                AgentResponse = string.Empty,
                Context = new Dictionary<string, object>
                {
                    ["source"] = "AgentFramework",
                    [FrameworkSessionStateKeyResolver.FrameworkAgentIdKey] = agent.Id,
                    [FrameworkSessionStateKeyResolver.FrameworkAgentNameKey] = stableAgentName,
                    [FrameworkSessionStateKeyResolver.FrameworkSessionStateKey] = stateJson
                }
            };

            await _sessionManager.AddEventAsync(conversationId, stateEvent);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to persist framework session state via AgentSessionStore adapter for ConversationId={ConversationId}",
                conversationId);
        }
    }

    public override async ValueTask<AgentSession> GetSessionAsync(
        AIAgent agent,
        string conversationId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);

        var stableAgentName = FrameworkSessionStateKeyResolver.ResolveAgentName(agent.Name, agent.Id);

        var persistedState = await GetPersistedStateAsync(conversationId, agent, cancellationToken);
        if (!string.IsNullOrWhiteSpace(persistedState))
        {
            try
            {
                using var doc = JsonDocument.Parse(persistedState);
                var restored = await agent.DeserializeSessionAsync(doc.RootElement, cancellationToken: cancellationToken);
                _logger.LogDebug(
                    "Framework session restored via AgentSessionStore adapter: {ConversationId}, AgentName={AgentName}",
                    conversationId,
                    stableAgentName);
                return restored;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to restore framework session state via AgentSessionStore adapter. Creating new session. ConversationId={ConversationId}",
                    conversationId);
            }
        }

        var session = await agent.CreateSessionAsync(cancellationToken);
        _logger.LogDebug(
            "New framework session created via AgentSessionStore adapter: {ConversationId}, AgentName={AgentName}",
            conversationId,
            stableAgentName);
        return session;
    }

    private async Task<string?> GetPersistedStateAsync(
        string conversationId,
        AIAgent agent,
        CancellationToken cancellationToken)
    {
        var stableAgentName = FrameworkSessionStateKeyResolver.ResolveAgentName(agent.Name, agent.Id);

        if (_sessionStore != null)
        {
            var sessionData = await _sessionStore.GetAsync(conversationId, cancellationToken);
            if (sessionData != null)
            {
                var runtimeKey = FrameworkSessionStateKeyResolver.BuildRuntimeKey(stableAgentName);
                if (sessionData.RuntimeSettings.TryGetValue(runtimeKey, out var stateFromStore))
                {
                    _logger.LogDebug(
                        "Framework session state found via stable runtime key: {ConversationId}, AgentName={AgentName}",
                        conversationId,
                        stableAgentName);
                    return stateFromStore;
                }

                var legacyRuntimeKey = FrameworkSessionStateKeyResolver.BuildLegacyRuntimeKey(agent.Id);
                if (sessionData.RuntimeSettings.TryGetValue(legacyRuntimeKey, out var legacyStateFromStore))
                {
                    _logger.LogDebug(
                        "Legacy framework session state found via agent id key: {ConversationId}, AgentId={AgentId}",
                        conversationId,
                        agent.Id);
                    return legacyStateFromStore;
                }
            }
        }

        var recentEvents = await _sessionManager.GetRecentEventsAsync(conversationId, 200);

        var stateEvent = recentEvents
            .Where(e => e.Context is not null)
            .OrderByDescending(e => e.Timestamp)
            .FirstOrDefault(e => FrameworkSessionStateKeyResolver.HasPersistedStateForAgent(e.Context, stableAgentName, agent.Id))
            ?? recentEvents
                .Where(e => e.Context is not null)
                .OrderByDescending(e => e.Timestamp)
                .FirstOrDefault(e => e.Context.ContainsKey(FrameworkSessionStateKeyResolver.FrameworkSessionStateKey)
                    && !e.Context.ContainsKey(FrameworkSessionStateKeyResolver.FrameworkAgentIdKey)
                    && !e.Context.ContainsKey(FrameworkSessionStateKeyResolver.FrameworkAgentNameKey));

        if (stateEvent?.Context is null)
        {
            return null;
        }

        if (stateEvent.Context.TryGetValue(FrameworkSessionStateKeyResolver.FrameworkSessionStateKey, out var value)
            && value is string serialized)
        {
            return serialized;
        }

        return null;
    }
}

internal static class FrameworkSessionStateKeyResolver
{
    internal const string FrameworkSessionStateKey = "frameworkSessionState";
    internal const string FrameworkAgentIdKey = "frameworkAgentId";
    internal const string FrameworkAgentNameKey = "frameworkAgentName";

    internal static string BuildRuntimeKey(string agentName)
    {
        var normalizedAgentName = string.IsNullOrWhiteSpace(agentName)
            ? "unknown-agent"
            : agentName.Trim().ToLowerInvariant();
        return $"{FrameworkSessionStateKey}:{normalizedAgentName}";
    }

    internal static string BuildLegacyRuntimeKey(string agentId)
        => $"{FrameworkSessionStateKey}:{agentId}";

    internal static string ResolveAgentName(string? agentName, string agentId)
        => string.IsNullOrWhiteSpace(agentName) ? agentId : agentName.Trim();

    internal static bool HasPersistedStateForAgent(
        Dictionary<string, object> context,
        string agentName,
        string agentId)
    {
        if (!context.ContainsKey(FrameworkSessionStateKey))
        {
            return false;
        }

        if (context.TryGetValue(FrameworkAgentNameKey, out var rawAgentName)
            && rawAgentName is not null
            && string.Equals(rawAgentName.ToString(), agentName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!context.TryGetValue(FrameworkAgentIdKey, out var rawAgentId) || rawAgentId is null)
        {
            return false;
        }

        return string.Equals(rawAgentId.ToString(), agentId, StringComparison.Ordinal);
    }
}