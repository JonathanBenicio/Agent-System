using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Interfaces;

/// <summary>
/// Service to broadcast workflow execution events via SignalR or other mechanisms.
/// </summary>
public interface IWorkflowEventBroadcaster
{
    Task BroadcastExecutionStarted(WorkflowExecution execution);
    Task BroadcastStepStarted(string executionId, WorkflowStepExecution step);
    Task BroadcastStepCompleted(string executionId, WorkflowStepExecution step);
    Task BroadcastStepFailed(string executionId, WorkflowStepExecution step);
    Task BroadcastExecutionCompleted(WorkflowExecution execution);
    Task BroadcastExecutionFailed(WorkflowExecution execution);
    Task BroadcastExecutionCancelled(WorkflowExecution execution);
}
