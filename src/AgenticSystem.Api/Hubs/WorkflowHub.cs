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

    public WorkflowHub(ILogger<WorkflowHub> logger)
    {
        _logger = logger;
    }

    public async Task SubscribeToWorkflow(string executionId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"workflow:{executionId}");
        _logger.LogDebug("Client {ConnectionId} subscribed to workflow {ExecutionId}",
            Context.ConnectionId, executionId);
    }

    public async Task UnsubscribeFromWorkflow(string executionId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"workflow:{executionId}");
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
            await _hubContext.Clients.Group($"workflow:{execution.Id}").SendAsync("ExecutionStarted", new
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

    public async Task BroadcastStepStarted(string executionId, WorkflowStepExecution step)
    {
        try
        {
            await _hubContext.Clients.Group($"workflow:{executionId}").SendAsync("StepStarted", new
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

    public async Task BroadcastStepCompleted(string executionId, WorkflowStepExecution step)
    {
        try
        {
            await _hubContext.Clients.Group($"workflow:{executionId}").SendAsync("StepCompleted", new
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

    public async Task BroadcastStepFailed(string executionId, WorkflowStepExecution step)
    {
        try
        {
            await _hubContext.Clients.Group($"workflow:{executionId}").SendAsync("StepFailed", new
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
            await _hubContext.Clients.Group($"workflow:{execution.Id}").SendAsync("ExecutionCompleted", new
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
            await _hubContext.Clients.Group($"workflow:{execution.Id}").SendAsync("ExecutionFailed", new
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
            await _hubContext.Clients.Group($"workflow:{execution.Id}").SendAsync("ExecutionCancelled", new
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
}
