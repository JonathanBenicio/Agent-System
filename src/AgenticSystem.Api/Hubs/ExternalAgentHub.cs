using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace AgenticSystem.Api.Hubs;

/// <summary>
/// SignalR Hub for BYOB (Bring Your Own Bot) / External Orchestration
/// Phase 3: Enables external systems to register themselves as agents, receive tasks, and report results in real-time.
/// </summary>
[Authorize]
public class ExternalAgentHub : Hub
{
    private readonly ILogger<ExternalAgentHub> _logger;

    public ExternalAgentHub(ILogger<ExternalAgentHub> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Registers an external bot instance with its capabilities.
    /// </summary>
    public async Task RegisterBot(string botName, string[] capabilities)
    {
        var connectionId = Context.ConnectionId;
        _logger.LogInformation("🤖 External Bot '{BotName}' registered with ConnectionId: {ConnectionId}", botName, connectionId);

        // Group by capabilities to route tasks appropriately
        foreach (var capability in capabilities)
        {
            await Groups.AddToGroupAsync(connectionId, $"capability:{capability}");
        }

        // Add to a general bots group
        await Groups.AddToGroupAsync(connectionId, "external_bots");

        await Clients.Caller.SendAsync("RegistrationConfirmed", new { 
            BotName = botName, 
            Status = "Active",
            CapabilitiesRegistered = capabilities.Length
        });
    }

    /// <summary>
    /// External bot reporting task completion.
    /// </summary>
    public async Task ReportTaskResult(string taskId, string status, string resultPayload)
    {
        _logger.LogInformation("✅ Task {TaskId} completed by external bot with status {Status}", taskId, status);
        
        // Broadcast the result to listeners or trigger internal state changes (e.g. WorkflowEngine)
        // For Phase 3, we just broadcast to the orchestrator group
        await Clients.Group("orchestrators").SendAsync("TaskResultReceived", new {
            TaskId = taskId,
            Status = status,
            Result = resultPayload,
            BotConnectionId = Context.ConnectionId
        });
    }

    /// <summary>
    /// Join as an orchestrator listener to monitor external bots.
    /// </summary>
    public async Task JoinAsOrchestrator()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "orchestrators");
        _logger.LogInformation("👁️ Client joined as External Orchestrator: {ConnectionId}", Context.ConnectionId);
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("🔌 ExternalAgentHub client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("🔌 ExternalAgentHub client disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}
