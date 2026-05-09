using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Interfaces;

/// <summary>
/// Persistence store for workflow definitions and execution states.
/// </summary>
public interface IWorkflowStore
{
    // Definitions
    Task SaveDefinitionAsync(WorkflowDefinition definition, CancellationToken ct = default);
    Task<WorkflowDefinition?> GetDefinitionAsync(string definitionId, CancellationToken ct = default);
    Task<IReadOnlyList<WorkflowDefinition>> ListDefinitionsAsync(int limit = 50, CancellationToken ct = default);
    Task DeleteDefinitionAsync(string definitionId, CancellationToken ct = default);

    // Executions
    Task SaveExecutionAsync(WorkflowExecution execution, CancellationToken ct = default);
    Task<WorkflowExecution?> GetExecutionAsync(string executionId, CancellationToken ct = default);
    Task<IReadOnlyList<WorkflowExecution>> ListExecutionsAsync(WorkflowExecutionStatus? status = null, int limit = 50, CancellationToken ct = default);
    Task DeleteExecutionAsync(string executionId, CancellationToken ct = default);
}
