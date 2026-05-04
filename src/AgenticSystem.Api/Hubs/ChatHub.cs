using Microsoft.AspNetCore.SignalR;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Api.Hubs;

/// <summary>
/// SignalR hub para comunicação real-time com agents.
/// </summary>
public class ChatHub : Hub
{
    private readonly IMetaAgent _metaAgent;
    private readonly ILogger<ChatHub> _logger;

    public ChatHub(IMetaAgent metaAgent, ILogger<ChatHub> logger)
    {
        _metaAgent = metaAgent;
        _logger = logger;
    }

    public async Task SendMessage(string userId, string message, string? targetAgent = null)
    {
        _logger.LogInformation("💬 Message from {UserId}: {Message} (target: {Target})", userId, message[..Math.Min(50, message.Length)], targetAgent ?? "auto");

        var userContext = new UserContext
        {
            UserId = userId,
            Name = userId,
            Language = "pt-BR"
        };

        // Notify client that processing started
        await Clients.Caller.SendAsync("ProcessingStarted", new { timestamp = DateTime.UtcNow });

        try
        {
            AgentResponse response;

            if (!string.IsNullOrWhiteSpace(targetAgent))
            {
                response = await _metaAgent.ProcessDirectRequestAsync(message, userContext, targetAgent);
            }
            else
            {
                response = await _metaAgent.ProcessRequestAsync(message, userContext);
            }

            await Clients.Caller.SendAsync("ReceiveMessage", new
            {
                content = response.Content,
                agentName = response.AgentName,
                agentTier = response.AgentTier,
                actions = response.ActionsPerformed,
                tools = response.ToolsUsed,
                success = response.Success,
                sessionId = response.SessionId,
                timestamp = response.Timestamp
            });
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
}
