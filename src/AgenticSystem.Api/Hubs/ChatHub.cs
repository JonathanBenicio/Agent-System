using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using AgenticSystem.Api.Helpers;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using System.Security.Claims;

namespace AgenticSystem.Api.Hubs;

/// <summary>
/// SignalR hub para comunicação real-time com agents.
/// Requer autenticação — identidade extraída do ClaimsPrincipal.
/// </summary>
[Authorize]
public class ChatHub : Hub
{
    private readonly IMetaAgent _metaAgent;
    private readonly ISessionStore _sessionStore;
    private readonly ILogger<ChatHub> _logger;
    private readonly ITenantContextAccessor _tenantContextAccessor;

    public ChatHub(
        IMetaAgent metaAgent, 
        ISessionStore sessionStore, 
        ILogger<ChatHub> logger,
        ITenantContextAccessor tenantContextAccessor)
    {
        _metaAgent = metaAgent;
        _sessionStore = sessionStore;
        _logger = logger;
        _tenantContextAccessor = tenantContextAccessor;
    }

    public async Task SendMessage(
        string message,
        string? targetAgent = null,
        string? provider = null,
        string? model = null,
        string? apiKey = null,
        string? sessionId = null,
        string? selectedRoomId = null)
    {
        // Identity from authenticated principal — never trust client-supplied userId
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? Context.User?.FindFirst("sub")?.Value
            ?? Context.User?.Identity?.Name
            ?? "authenticated-user";

        if (!await SessionAccessValidator.CanAccessAsync(
                _sessionStore,
                sessionId,
                userId,
                _tenantContextAccessor.CurrentTenantId,
                Context.ConnectionAborted))
        {
            await Clients.Caller.SendAsync("ReceiveError", new
            {
                error = "Session not found or access denied.",
                timestamp = DateTime.UtcNow
            }, Context.ConnectionAborted);
            return;
        }

        _logger.LogInformation("💬 Message from {UserId}: {Message} (target: {Target}, session: {SessionId}, room: {RoomId})", userId, message[..Math.Min(50, message.Length)], targetAgent ?? "auto", sessionId ?? "new", selectedRoomId ?? "none");

        var preferences = BuildLlmPreferences(provider, model, apiKey);
        if (!string.IsNullOrWhiteSpace(selectedRoomId))
        {
            preferences["rag.knowledgeRoomId"] = selectedRoomId;
        }

        var userContext = new UserContext
        {
            UserId = userId,
            Name = userId,
            TenantId = _tenantContextAccessor.CurrentTenantId,
            Language = "pt-BR",
            Preferences = preferences
        };

        // Notify client that processing started
        await Clients.Caller.SendAsync("ProcessingStarted", new { timestamp = DateTime.UtcNow });

        try
        {
            await foreach (var streamEvent in ResolveStream(message, userContext, targetAgent, sessionId, Context.ConnectionAborted))
            {
                await Clients.Caller.SendAsync("StreamEvent", streamEvent, Context.ConnectionAborted);

                if (streamEvent.Type == AgentStreamEventType.SessionCompleted)
                {
                    await Clients.Caller.SendAsync("ReceiveMessage", new
                    {
                        content = streamEvent.Message,
                        agentName = streamEvent.AgentName,
                        agentTier = streamEvent.Data.TryGetValue("agentTier", out var tier) ? tier?.ToString() : null,
                        actions = streamEvent.Data.TryGetValue("actions", out var actions) ? actions : null,
                        tools = streamEvent.Data.TryGetValue("tools", out var tools) ? tools : null,
                        success = streamEvent.Data.TryGetValue("success", out var success) && success is bool ok && ok,
                        sessionId = streamEvent.SessionId,
                        timestamp = streamEvent.Timestamp,
                        memoryInjected = streamEvent.Data.TryGetValue("memoryInjected", out var mi) && mi is bool b && b,
                        citations = streamEvent.Data.TryGetValue("citations", out var c) ? c : null
                    }, Context.ConnectionAborted);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error processing SignalR message");
            await Clients.Caller.SendAsync("ReceiveError", new
            {
                error = "Erro ao processar mensagem.",
                timestamp = DateTime.UtcNow
            });
        }
    }

    public async Task JoinSession(string sessionId)
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? Context.User?.FindFirst("sub")?.Value
            ?? Context.User?.Identity?.Name
            ?? "authenticated-user";

        var session = await _sessionStore.GetAsync(sessionId, Context.ConnectionAborted);
        if (session is null || session.UserId != userId ||
            !string.Equals(session.TenantId, _tenantContextAccessor.CurrentTenantId, StringComparison.OrdinalIgnoreCase))
        {
            await Clients.Caller.SendAsync("JoinSessionError", new
            {
                error = "Session not found or access denied.",
                sessionId
            });
            return;
        }

        _logger.LogInformation("📂 Client {ConnectionId} joined session {SessionId}", Context.ConnectionId, sessionId);

        await Clients.Caller.SendAsync("SessionJoined", new
        {
            sessionId = session.Id,
            title = session.RuntimeSettings.TryGetValue("title", out var t) ? t : null,
            startedAt = session.StartedAt,
            messageCount = session.Events.Count,
            summary = session.Summary != null ? SessionDtoMapper.ToSummary(session.Summary) : null,
            insights = session.Insights != null ? SessionDtoMapper.ToInsights(session.Insights) : null
        });
    }

    public Task SendMessageStream(
        string message,
        string? targetAgent = null,
        string? provider = null,
        string? model = null,
        string? apiKey = null,
        string? sessionId = null,
        string? selectedRoomId = null)
        => SendMessage(message, targetAgent, provider, model, apiKey, sessionId, selectedRoomId);

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("🔌 Client connected: {ConnectionId}", Context.ConnectionId);
        
        // Add connection to tenant group for targeted notifications (like LlmCatalogUpdated)
        var tenantId = _tenantContextAccessor.CurrentTenantId;
        await Groups.AddToGroupAsync(Context.ConnectionId, $"tenant:{tenantId}");
        
        await Clients.Caller.SendAsync("Connected", new
        {
            connectionId = Context.ConnectionId,
            timestamp = DateTime.UtcNow
        });
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("🔌 Client disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    private static Dictionary<string, object> BuildLlmPreferences(string? provider, string? model, string? apiKey)
    {
        var preferences = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(provider))
        {
            preferences["llm.request.provider"] = provider;
            preferences["llm.session.provider"] = provider;
            preferences["llm.provider"] = provider;
        }

        if (!string.IsNullOrWhiteSpace(model))
        {
            preferences["llm.request.model"] = model;
            preferences["llm.session.model"] = model;
            preferences["llm.model"] = model;
        }

        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            preferences["llm.request.apiKey"] = apiKey;
            preferences["llm.session.apiKey"] = apiKey;
            preferences["llm.apiKey"] = apiKey;
        }

        return preferences;
    }

    private IAsyncEnumerable<AgentStreamEvent> ResolveStream(string message, UserContext userContext, string? targetAgent, string? sessionId, CancellationToken ct)
    {
        return !string.IsNullOrWhiteSpace(targetAgent)
            ? _metaAgent.ProcessDirectRequestStreamAsync(message, userContext, targetAgent, sessionId, ct)
            : _metaAgent.ProcessRequestStreamAsync(message, userContext, sessionId, ct);
    }
}
