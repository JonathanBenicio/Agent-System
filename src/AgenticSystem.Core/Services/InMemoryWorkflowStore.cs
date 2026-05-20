using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Services;

public class InMemoryWorkflowStore : IWorkflowStore
{
    private readonly List<WorkflowDefinition> _definitions = new();
    private readonly List<WorkflowExecution> _executions = new();

    public Task SaveDefinitionAsync(string tenantId, WorkflowDefinition definition, CancellationToken ct = default)
    {
        var existing = _definitions.FirstOrDefault(d => d.Id == definition.Id);
        if (existing != null) _definitions.Remove(existing);
        _definitions.Add(definition);
        return Task.CompletedTask;
    }

    public Task<WorkflowDefinition?> GetDefinitionAsync(string tenantId, string definitionId, CancellationToken ct = default)
    {
        return Task.FromResult(_definitions.FirstOrDefault(d => d.Id == definitionId));
    }

    public Task<IReadOnlyList<WorkflowDefinition>> ListDefinitionsAsync(string tenantId, int limit = 50, CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<WorkflowDefinition>>(_definitions.OrderByDescending(d => d.CreatedAt).Take(limit).ToList());
    }

    public Task DeleteDefinitionAsync(string tenantId, string definitionId, CancellationToken ct = default)
    {
        var existing = _definitions.FirstOrDefault(d => d.Id == definitionId);
        if (existing != null) _definitions.Remove(existing);
        return Task.CompletedTask;
    }

    public Task SaveExecutionAsync(string tenantId, WorkflowExecution execution, CancellationToken ct = default)
    {
        var existing = _executions.FirstOrDefault(e => e.Id == execution.Id);
        if (existing != null) _executions.Remove(existing);
        _executions.Add(execution);
        return Task.CompletedTask;
    }

    public Task<WorkflowExecution?> GetExecutionAsync(string tenantId, string executionId, CancellationToken ct = default)
    {
        return Task.FromResult(_executions.FirstOrDefault(e => e.Id == executionId));
    }

    public Task<IReadOnlyList<WorkflowExecution>> ListExecutionsAsync(string tenantId, WorkflowExecutionStatus? status = null, int limit = 50, CancellationToken ct = default)
    {
        var query = _executions.AsQueryable();
        if (status.HasValue) query = query.Where(e => e.Status == status.Value);
        return Task.FromResult<IReadOnlyList<WorkflowExecution>>(query.OrderByDescending(e => e.StartedAt).Take(limit).ToList());
    }

    public Task DeleteExecutionAsync(string tenantId, string executionId, CancellationToken ct = default)
    {
        var existing = _executions.FirstOrDefault(e => e.Id == executionId);
        if (existing != null) _executions.Remove(existing);
        return Task.CompletedTask;
    }
}
