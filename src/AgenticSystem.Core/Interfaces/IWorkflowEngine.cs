using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Interfaces;

/// <summary>
/// Workflow engine for orchestrating multi-step agent workflows
/// with support for parallel execution, branching, compensation, and scheduling.
/// </summary>
public interface IWorkflowEngine
{
    /// <summary>
    /// Starts execution of a workflow definition.
    /// </summary>
    Task<WorkflowExecution> StartAsync(
        string tenantId,
        WorkflowDefinition workflow,
        Dictionary<string, object>? initialVariables = null,
        string? initiatedBy = null,
        CancellationToken ct = default);

    /// <summary>
    /// Resumes a paused or waiting workflow.
    /// </summary>
    Task<WorkflowExecution> ResumeAsync(
        string tenantId,
        string executionId,
        Dictionary<string, object>? additionalInput = null,
        CancellationToken ct = default);

    /// <summary>Processes one execution whose database lease has already been claimed.</summary>
    Task ProcessClaimedExecutionAsync(WorkflowExecutionClaim claim, CancellationToken ct = default);

    /// <summary>Approves the pending human-approval step and resumes the workflow.</summary>
    Task<WorkflowExecution> ApproveAsync(
        string tenantId,
        string executionId,
        string approvedBy,
        CancellationToken ct = default);

    Task<WorkflowExecution> ApproveStepAsync(string tenantId, string executionId, string stepId, string approvedBy, CancellationToken ct = default);

    /// <summary>Rejects the pending human-approval step and terminates the workflow.</summary>
    Task<WorkflowExecution> RejectAsync(
        string tenantId,
        string executionId,
        string rejectedBy,
        string? reason = null,
        CancellationToken ct = default);

    Task<WorkflowExecution> RejectStepAsync(string tenantId, string executionId, string stepId, string rejectedBy, string? reason = null, CancellationToken ct = default);

    /// <summary>
    /// Cancels a running workflow. Triggers compensation for completed steps if configured.
    /// </summary>
    Task<WorkflowExecution> CancelAsync(
        string tenantId,
        string executionId,
        string? reason = null,
        CancellationToken ct = default);

    /// <summary>
    /// Returns the current state of a workflow execution.
    /// </summary>
    Task<WorkflowExecution?> GetExecutionAsync(
        string tenantId,
        string executionId,
        CancellationToken ct = default);

    /// <summary>
    /// Lists active and recent workflow executions.
    /// </summary>
    Task<IReadOnlyList<WorkflowExecution>> ListExecutionsAsync(
        string tenantId,
        WorkflowExecutionStatus? statusFilter = null,
        int limit = 20,
        CancellationToken ct = default);
}
