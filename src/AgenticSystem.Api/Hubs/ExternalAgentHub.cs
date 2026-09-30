using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using AgenticSystem.Core.Interfaces;

namespace AgenticSystem.Api.Hubs;

/// <summary>
/// SignalR Hub for BYOB (Bring Your Own Bot) / External Orchestration
/// Phase 3: Enables external systems to register themselves as agents, receive tasks, and report results in real-time.
/// </summary>
[Authorize]
public class ExternalAgentHub : Hub
{
    private readonly ILogger<ExternalAgentHub> _logger;
    private readonly ITenantContextAccessor _tenantContextAccessor;

    public ExternalAgentHub(ILogger<ExternalAgentHub> logger, ITenantContextAccessor tenantContextAccessor)
    {
        _logger = logger;
        _tenantContextAccessor = tenantContextAccessor;
    }

    /// <summary>
    /// Registers an external bot instance with its capabilities.
    /// </summary>
    public async Task RegisterBot(string botName, string[] capabilities)
    {
        var tenantId = RequireTenantId();
        var connectionId = Context.ConnectionId;
        _logger.LogInformation("🤖 External Bot '{BotName}' registered with ConnectionId: {ConnectionId}", botName, connectionId);

        // Group by capabilities to route tasks appropriately
        foreach (var capability in capabilities)
        {
            await Groups.AddToGroupAsync(connectionId, $"tenant:{tenantId}:capability:{capability}");
        }

        // Add to a general bots group
        await Groups.AddToGroupAsync(connectionId, $"tenant:{tenantId}:external_bots");

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
        var tenantId = RequireTenantId();
        _logger.LogInformation("✅ Task {TaskId} completed by external bot with status {Status}", taskId, status);
        
        // Broadcast the result to listeners or trigger internal state changes (e.g. WorkflowEngine)
        // For Phase 3, we just broadcast to the orchestrator group
        await Clients.Group($"tenant:{tenantId}:orchestrators").SendAsync("TaskResultReceived", new {
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
        var tenantId = RequireTenantId();
        await Groups.AddToGroupAsync(Context.ConnectionId, $"tenant:{tenantId}:orchestrators");
        _logger.LogInformation("👁️ Client joined as External Orchestrator: {ConnectionId}", Context.ConnectionId);
    }

    private string RequireTenantId() => _tenantContextAccessor.CurrentTenantId
        ?? throw new HubException("Tenant identity is required.");

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
