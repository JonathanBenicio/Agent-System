using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Interfaces;

/// <summary>
/// Service to broadcast workflow execution events via SignalR or other mechanisms.
/// </summary>
public interface IWorkflowEventBroadcaster
{
    Task BroadcastExecutionStarted(WorkflowExecution execution);
    Task BroadcastStepStarted(string tenantId, string executionId, WorkflowStepExecution step);
    Task BroadcastApprovalRequested(WorkflowExecution execution, WorkflowStepExecution step);
    Task BroadcastStepCompleted(string tenantId, string executionId, WorkflowStepExecution step);
    Task BroadcastStepFailed(string tenantId, string executionId, WorkflowStepExecution step);
    Task BroadcastExecutionCompleted(WorkflowExecution execution);
    Task BroadcastExecutionFailed(WorkflowExecution execution);
    Task BroadcastExecutionCancelled(WorkflowExecution execution);
}
