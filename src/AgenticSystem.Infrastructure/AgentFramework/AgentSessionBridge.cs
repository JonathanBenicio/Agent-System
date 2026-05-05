using System.Text.Json;
using Microsoft.Extensions.Logging;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using FrameworkAgent = Microsoft.Agents.AI.AIAgent;
using FrameworkAgentSession = Microsoft.Agents.AI.AgentSession;

namespace AgenticSystem.Infrastructure.AgentFramework;

/// <summary>
/// Ponte entre ISessionManager e AgentSession do Microsoft Agent Framework.
/// O estado de sessão do framework é persistido via ISessionStore.RuntimeSettings,
/// usando chaves dedicadas (frameworkSessionState:{agentId}) — não mais como eventos falsos.
/// Fallback: busca em eventos para compatibilidade com sessões pré-Fase 3.
/// </summary>
public class AgentSessionBridge
{
    private readonly ISessionManager _sessionManager;
    private readonly ISessionStore? _sessionStore;
    private readonly ILogger<AgentSessionBridge> _logger;

    private const string FrameworkSessionStateKey = "frameworkSessionState";
    private const string FrameworkAgentIdKey = "frameworkAgentId";

    public AgentSessionBridge(
        ISessionManager sessionManager,
        ILogger<AgentSessionBridge> logger,
        ISessionStore? sessionStore = null)
    {
        _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _sessionStore = sessionStore;
    }

    /// <summary>
    /// Obtém ou cria AgentSession a partir de estado persistido na própria sessão de negócio.
    /// </summary>
    public async Task<FrameworkAgentSession> GetOrCreateFrameworkSessionAsync(
        FrameworkAgent agent,
        string sessionId,
        CancellationToken ct = default)
    {
        var persistedState = await GetPersistedStateAsync(sessionId, agent.Id);
        if (!string.IsNullOrWhiteSpace(persistedState))
        {
            try
            {
                using var doc = JsonDocument.Parse(persistedState);
                var restored = await agent.DeserializeSessionAsync(doc.RootElement, cancellationToken: ct);
                _logger.LogDebug("Framework session restored from persisted state: {SessionId}, AgentId={AgentId}", sessionId, agent.Id);
                return restored;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to restore framework session from persisted state. Creating new session. SessionId={SessionId}", sessionId);
            }
        }

        var session = await agent.CreateSessionAsync(ct);
        _logger.LogDebug("New framework session created: {SessionId}, AgentId={AgentId}", sessionId, agent.Id);
        return session;
    }

    /// <summary>
    /// Sincroniza resultado do Agent Framework de volta para o ISessionManager.
    /// </summary>
    public async Task SyncResponseAsync(string sessionId, string agentName, string userInput, AgentResponse response)
    {
        var agentEvent = new AgentEvent
        {
            SessionId = sessionId,
            AgentName = agentName,
            UserInput = userInput,
            AgentResponse = response.Content,
            ActionsPerformed = response.ActionsPerformed,
            ToolsUsed = response.ToolsUsed,
            Context = new Dictionary<string, object>
            {
                ["source"] = "AgentFramework",
                ["success"] = response.Success
            }
        };

        await _sessionManager.AddEventAsync(sessionId, agentEvent);
    }

    /// <summary>
    /// Persiste o estado serializado do AgentSession.
    /// Prioriza ISessionStore.RuntimeSettings; fallback para evento se ISessionStore indisponível.
    /// </summary>
    public async Task PersistFrameworkSessionAsync(
        string sessionId,
        FrameworkAgent agent,
        FrameworkAgentSession session,
        CancellationToken ct = default)
    {
        try
        {
            var serialized = await agent.SerializeSessionAsync(session, cancellationToken: ct);
            var stateJson = serialized.GetRawText();

            // Prioridade: ISessionStore (RuntimeSettings) — acesso direto, sem poluir eventos
            if (_sessionStore != null)
            {
                var sessionData = await _sessionStore.GetAsync(sessionId, ct);
                if (sessionData != null)
                {
                    var runtimeKey = $"{FrameworkSessionStateKey}:{agent.Id}";
                    sessionData.RuntimeSettings[runtimeKey] = stateJson;
                    await _sessionStore.SaveAsync(sessionData, ct);
                    _logger.LogDebug("Framework session state persisted via ISessionStore: {SessionId}, AgentId={AgentId}", sessionId, agent.Id);
                    return;
                }
            }

            // Fallback: persistir como evento (compatibilidade pré-Fase 3)
            var stateEvent = new AgentEvent
            {
                SessionId = sessionId,
                AgentName = "AgentFramework.Session",
                UserInput = "[framework-session-state]",
                AgentResponse = string.Empty,
                Context = new Dictionary<string, object>
                {
                    ["source"] = "AgentFramework",
                    [FrameworkAgentIdKey] = agent.Id,
                    [FrameworkSessionStateKey] = stateJson
                }
            };

            await _sessionManager.AddEventAsync(sessionId, stateEvent);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist framework session state for SessionId={SessionId}", sessionId);
        }
    }

    public void RemoveSession(string sessionId)
    {
    }

    public int ActiveSessionCount => 0;

    private async Task<string?> GetPersistedStateAsync(string sessionId, string agentId)
    {
        // Prioridade: ISessionStore.RuntimeSettings (Fase 3+)
        if (_sessionStore != null)
        {
            var sessionData = await _sessionStore.GetAsync(sessionId);
            if (sessionData != null)
            {
                var runtimeKey = $"{FrameworkSessionStateKey}:{agentId}";
                if (sessionData.RuntimeSettings.TryGetValue(runtimeKey, out var stateFromStore))
                {
                    _logger.LogDebug("Framework session state found via ISessionStore: {SessionId}, AgentId={AgentId}", sessionId, agentId);
                    return stateFromStore;
                }
            }
        }

        // Fallback: busca em eventos (compatibilidade pré-Fase 3)
        var recentEvents = await _sessionManager.GetRecentEventsAsync(sessionId, 200);

        var stateEvent = recentEvents
            .Where(e => e.Context is not null)
            .OrderByDescending(e => e.Timestamp)
            .FirstOrDefault(e => HasPersistedStateForAgent(e.Context, agentId))
            ?? recentEvents
                .Where(e => e.Context is not null)
                .OrderByDescending(e => e.Timestamp)
                .FirstOrDefault(e => e.Context.ContainsKey(FrameworkSessionStateKey)
                    && !e.Context.ContainsKey(FrameworkAgentIdKey));

        if (stateEvent?.Context is null)
            return null;

        if (stateEvent.Context.TryGetValue(FrameworkSessionStateKey, out var value) && value is string serialized)
            return serialized;

        return null;
    }

    private static bool HasPersistedStateForAgent(Dictionary<string, object> context, string agentId)
    {
        if (!context.ContainsKey(FrameworkSessionStateKey))
        {
            return false;
        }

        if (!context.TryGetValue(FrameworkAgentIdKey, out var rawAgentId) || rawAgentId is null)
        {
            return false;
        }

        return string.Equals(rawAgentId.ToString(), agentId, StringComparison.Ordinal);
    }
}
