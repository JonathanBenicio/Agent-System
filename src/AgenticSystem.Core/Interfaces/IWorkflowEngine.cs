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
        WorkflowDefinition workflow,
        Dictionary<string, object>? initialVariables = null,
        string? initiatedBy = null,
        CancellationToken ct = default);

    /// <summary>
    /// Resumes a paused or waiting workflow.
    /// </summary>
    Task<WorkflowExecution> ResumeAsync(
        string executionId,
        Dictionary<string, object>? additionalInput = null,
        CancellationToken ct = default);

    /// <summary>
    /// Cancels a running workflow. Triggers compensation for completed steps if configured.
    /// </summary>
    Task<WorkflowExecution> CancelAsync(
        string executionId,
        string? reason = null,
        CancellationToken ct = default);

    /// <summary>
    /// Returns the current state of a workflow execution.
    /// </summary>
    Task<WorkflowExecution?> GetExecutionAsync(
        string executionId,
        CancellationToken ct = default);

    /// <summary>
    /// Lists active and recent workflow executions.
    /// </summary>
    Task<IReadOnlyList<WorkflowExecution>> ListExecutionsAsync(
        WorkflowExecutionStatus? statusFilter = null,
        int limit = 20,
        CancellationToken ct = default);
}
