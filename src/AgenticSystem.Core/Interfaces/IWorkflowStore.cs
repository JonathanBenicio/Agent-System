using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Interfaces;

/// <summary>
/// Persistence store for workflow definitions and execution states.
/// </summary>
public interface IWorkflowStore
{
    // Definitions
    Task SaveDefinitionAsync(string tenantId, WorkflowDefinition definition, CancellationToken ct = default);
    Task<WorkflowDefinition?> GetDefinitionAsync(string tenantId, string definitionId, CancellationToken ct = default);
    Task<IReadOnlyList<WorkflowDefinition>> ListDefinitionsAsync(string tenantId, int limit = 50, CancellationToken ct = default);
    Task DeleteDefinitionAsync(string tenantId, string definitionId, CancellationToken ct = default);

    // Executions
    Task SaveExecutionAsync(string tenantId, WorkflowExecution execution, CancellationToken ct = default);
    Task<WorkflowExecution?> GetExecutionAsync(string tenantId, string executionId, CancellationToken ct = default);
    Task<IReadOnlyList<WorkflowExecution>> ListExecutionsAsync(string tenantId, WorkflowExecutionStatus? status = null, int limit = 50, CancellationToken ct = default);
    Task DeleteExecutionAsync(string tenantId, string executionId, CancellationToken ct = default);
}
