using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
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

    public ChatHub(IMetaAgent metaAgent, ISessionStore sessionStore, ILogger<ChatHub> logger)
    {
        _metaAgent = metaAgent;
        _sessionStore = sessionStore;
        _logger = logger;
    }

    public async Task SendMessage(
        string message,
        string? targetAgent = null,
        string? provider = null,
        string? model = null,
        string? apiKey = null)
    {
        // Identity from authenticated principal — never trust client-supplied userId
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? Context.User?.FindFirst("sub")?.Value
            ?? Context.User?.Identity?.Name
            ?? "authenticated-user";

        _logger.LogInformation("💬 Message from {UserId}: {Message} (target: {Target})", userId, message[..Math.Min(50, message.Length)], targetAgent ?? "auto");

        var userContext = new UserContext
        {
            UserId = userId,
            Name = userId,
            Language = "pt-BR",
            Preferences = BuildLlmPreferences(provider, model, apiKey)
        };

        // Notify client that processing started
        await Clients.Caller.SendAsync("ProcessingStarted", new { timestamp = DateTime.UtcNow });

        try
        {
            await foreach (var streamEvent in ResolveStream(message, userContext, targetAgent, Context.ConnectionAborted))
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
                        memoryInjected = streamEvent.Data.TryGetValue("memoryInjected", out var mi) && mi is bool b && b
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
        if (session is null || session.UserId != userId)
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

        foreach (var evt in session.Events.OrderBy(e => e.Timestamp))
        {
            var memoryInjected = evt.Context.TryGetValue("memory_injected", out var mi) && mi is bool b && b;

            await Clients.Caller.SendAsync("ReceiveMessage", new
            {
                content = evt.UserInput,
                agentName = (string?)null,
                agentTier = (string?)null,
                actions = (object?)null,
                tools = (object?)null,
                success = true,
                sessionId = evt.SessionId,
                timestamp = evt.Timestamp,
                isHistory = true,
                memoryInjected
            });

            await Clients.Caller.SendAsync("ReceiveMessage", new
            {
                content = evt.AgentResponse,
                agentName = evt.AgentName,
                agentTier = evt.AgentTier.ToString(),
                actions = evt.ActionsPerformed.Count > 0 ? evt.ActionsPerformed : null,
                tools = evt.ToolsUsed.Count > 0 ? evt.ToolsUsed : null,
                success = true,
                sessionId = evt.SessionId,
                timestamp = evt.Timestamp,
                isHistory = true
            });
        }
    }

    public Task SendMessageStream(
        string message,
        string? targetAgent = null,
        string? provider = null,
        string? model = null,
        string? apiKey = null)
        => SendMessage(message, targetAgent, provider, model, apiKey);

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("🔌 Client connected: {ConnectionId}", Context.ConnectionId);
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

    private IAsyncEnumerable<AgentStreamEvent> ResolveStream(string message, UserContext userContext, string? targetAgent, CancellationToken ct)
    {
        return !string.IsNullOrWhiteSpace(targetAgent)
            ? _metaAgent.ProcessDirectRequestStreamAsync(message, userContext, targetAgent, ct)
            : _metaAgent.ProcessRequestStreamAsync(message, userContext, ct);
    }
}
