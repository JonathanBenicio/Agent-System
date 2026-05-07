using System.Text.Json;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Infrastructure.AgentFramework;

/// <summary>
/// [PHASE 2] Adapter simplificado do store de sessão.
/// Persiste estados do framework na sessão usando apenas agent name como chave (sem fallbacks legados).
/// </summary>
public sealed class SimpleSessionStoreAdapter : AgentSessionStore
{
    private readonly ISessionStore _sessionStore;
    private readonly ILogger<SimpleSessionStoreAdapter> _logger;
    private const string FrameworkStateKey = "frameworkSessionState";

    public SimpleSessionStoreAdapter(
        ISessionStore sessionStore,
        ILogger<SimpleSessionStoreAdapter> logger)
    {
        _sessionStore = sessionStore ?? throw new ArgumentNullException(nameof(sessionStore));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
            var serialized = await agent.SerializeSessionAsync(session, cancellationToken: cancellationToken);
            var stateJson = serialized.GetRawText();
            var runtimeKey = BuildRuntimeKey(agent.Name);

            var sessionData = await _sessionStore.GetAsync(conversationId, cancellationToken);
            if (sessionData is not null)
            {
                sessionData.RuntimeSettings[runtimeKey] = stateJson;
                await _sessionStore.SaveAsync(sessionData, cancellationToken);
                
                _logger.LogDebug(
                    "Framework session persisted: ConversationId={ConversationId}, Agent={AgentName}, Key={RuntimeKey}",
                    conversationId,
                    agent.Name,
                    runtimeKey);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist framework session for ConversationId={ConversationId}", conversationId);
        }
    }

    public override async ValueTask<AgentSession> GetSessionAsync(
        AIAgent agent,
        string conversationId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);

        var runtimeKey = BuildRuntimeKey(agent.Name);
        var persistedState = await GetPersistedStateAsync(conversationId, runtimeKey, cancellationToken);

        if (!string.IsNullOrWhiteSpace(persistedState))
        {
            try
            {
                using var doc = JsonDocument.Parse(persistedState);
                var restored = await agent.DeserializeSessionAsync(doc.RootElement, cancellationToken: cancellationToken);
                
                _logger.LogDebug(
                    "Framework session restored: ConversationId={ConversationId}, Agent={AgentName}",
                    conversationId,
                    agent.Name);
                
                return restored;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to restore framework session, creating new one. ConversationId={ConversationId}", conversationId);
            }
        }

        var newSession = await agent.CreateSessionAsync(cancellationToken);
        
        _logger.LogDebug(
            "New framework session created: ConversationId={ConversationId}, Agent={AgentName}",
            conversationId,
            agent.Name);
        
        return newSession;
    }

    private async Task<string?> GetPersistedStateAsync(
        string conversationId,
        string runtimeKey,
        CancellationToken cancellationToken)
    {
        var sessionData = await _sessionStore.GetAsync(conversationId, cancellationToken);
        if (sessionData?.RuntimeSettings.TryGetValue(runtimeKey, out var state) == true)
        {
            return state as string;
        }

        return null;
    }

    private static string BuildRuntimeKey(string? agentName)
    {
        if (string.IsNullOrWhiteSpace(agentName))
            return $"{FrameworkStateKey}:default";

        var normalized = agentName.Trim().ToLowerInvariant();
        return $"{FrameworkStateKey}:{normalized}";
    }
}
