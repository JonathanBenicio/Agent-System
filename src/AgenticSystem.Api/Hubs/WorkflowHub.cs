using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Api.Hubs;

/// <summary>
/// SignalR Hub for real-time workflow execution events.
/// Events: ExecutionStarted, StepStarted, StepCompleted, StepFailed, ExecutionCompleted, ExecutionFailed, ExecutionCancelled
/// </summary>
[Authorize]
public class WorkflowHub : Hub
{
    private readonly ILogger<WorkflowHub> _logger;
    private readonly ITenantContextAccessor _tenantContextAccessor;

    public WorkflowHub(ILogger<WorkflowHub> logger, ITenantContextAccessor tenantContextAccessor)
    {
        _logger = logger;
        _tenantContextAccessor = tenantContextAccessor;
    }

    public async Task SubscribeToWorkflow(string executionId)
    {
        var tenantId = _tenantContextAccessor.CurrentTenantId;
        if (string.IsNullOrWhiteSpace(tenantId)) throw new HubException("Tenant identity is required.");
        await Groups.AddToGroupAsync(Context.ConnectionId, $"tenant:{tenantId}:workflow:{executionId}");
        _logger.LogDebug("Client {ConnectionId} subscribed to workflow {ExecutionId}",
            Context.ConnectionId, executionId);
    }

    public async Task UnsubscribeFromWorkflow(string executionId)
    {
        var tenantId = _tenantContextAccessor.CurrentTenantId;
        if (!string.IsNullOrWhiteSpace(tenantId))
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"tenant:{tenantId}:workflow:{executionId}");
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("🔌 WorkflowHub client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("🔌 WorkflowHub client disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}

public class SignalRWorkflowEventBroadcaster : IWorkflowEventBroadcaster
{
    private readonly IHubContext<WorkflowHub> _hubContext;
    private readonly ILogger<SignalRWorkflowEventBroadcaster> _logger;

    public SignalRWorkflowEventBroadcaster(IHubContext<WorkflowHub> hubContext, ILogger<SignalRWorkflowEventBroadcaster> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task BroadcastExecutionStarted(WorkflowExecution execution)
    {
        try
        {
            await _hubContext.Clients.Group(WorkflowGroup(execution.TenantId, execution.Id)).SendAsync("ExecutionStarted", new
            {
                execution.Id,
                execution.WorkflowId,
                execution.WorkflowName,
                execution.Status,
                execution.StartedAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to broadcast ExecutionStarted for {ExecutionId}", execution.Id);
        }
    }

    public async Task BroadcastStepStarted(string tenantId, string executionId, WorkflowStepExecution step)
    {
        try
        {
            await _hubContext.Clients.Group(WorkflowGroup(tenantId, executionId)).SendAsync("StepStarted", new
            {
                executionId,
                step.StepId,
                step.StepName,
                step.Status,
                step.StartedAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to broadcast StepStarted for {ExecutionId}/{StepId}", executionId, step.StepId);
        }
    }

    public async Task BroadcastApprovalRequested(WorkflowExecution execution, WorkflowStepExecution step)
    {
        try
        {
            await _hubContext.Clients.Group(WorkflowGroup(execution.TenantId, execution.Id)).SendAsync("ApprovalRequested", new
            {
                executionId = execution.Id,
                workflowId = execution.WorkflowId,
                executionStatus = execution.Status,
                step.StepId,
                step.StepName,
                stepStatus = step.Status,
                step.StartedAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to broadcast ApprovalRequested for {ExecutionId}/{StepId}", execution.Id, step.StepId);
        }
    }

    public async Task BroadcastStepCompleted(string tenantId, string executionId, WorkflowStepExecution step)
    {
        try
        {
            await _hubContext.Clients.Group(WorkflowGroup(tenantId, executionId)).SendAsync("StepCompleted", new
            {
                executionId,
                step.StepId,
                step.StepName,
                step.Status,
                step.Output,
                step.CompletedAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to broadcast StepCompleted for {ExecutionId}/{StepId}", executionId, step.StepId);
        }
    }

    public async Task BroadcastStepFailed(string tenantId, string executionId, WorkflowStepExecution step)
    {
        try
        {
            await _hubContext.Clients.Group(WorkflowGroup(tenantId, executionId)).SendAsync("StepFailed", new
            {
                executionId,
                step.StepId,
                step.StepName,
                step.Status,
                step.ErrorMessage,
                step.CompletedAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to broadcast StepFailed for {ExecutionId}/{StepId}", executionId, step.StepId);
        }
    }

    public async Task BroadcastExecutionCompleted(WorkflowExecution execution)
    {
        try
        {
            await _hubContext.Clients.Group(WorkflowGroup(execution.TenantId, execution.Id)).SendAsync("ExecutionCompleted", new
            {
                execution.Id,
                execution.WorkflowId,
                execution.WorkflowName,
                execution.Status,
                execution.CompletedAt,
                execution.Duration
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to broadcast ExecutionCompleted for {ExecutionId}", execution.Id);
        }
    }

    public async Task BroadcastExecutionFailed(WorkflowExecution execution)
    {
        try
        {
            await _hubContext.Clients.Group(WorkflowGroup(execution.TenantId, execution.Id)).SendAsync("ExecutionFailed", new
            {
                execution.Id,
                execution.WorkflowId,
                execution.WorkflowName,
                execution.Status,
                execution.ErrorMessage,
                execution.CompletedAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to broadcast ExecutionFailed for {ExecutionId}", execution.Id);
        }
    }

    public async Task BroadcastExecutionCancelled(WorkflowExecution execution)
    {
        try
        {
            await _hubContext.Clients.Group(WorkflowGroup(execution.TenantId, execution.Id)).SendAsync("ExecutionCancelled", new
            {
                execution.Id,
                execution.WorkflowId,
                execution.WorkflowName,
                execution.Status,
                execution.ErrorMessage,
                execution.CompletedAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to broadcast ExecutionCancelled for {ExecutionId}", execution.Id);
        }
    }

    private static string WorkflowGroup(string tenantId, string executionId) => $"tenant:{tenantId}:workflow:{executionId}";
}
