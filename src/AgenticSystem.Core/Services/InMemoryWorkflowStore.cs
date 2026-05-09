using System.Collections.Concurrent;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Services;

public class InMemoryWorkflowStore : IWorkflowStore
{
    private readonly ConcurrentDictionary<string, WorkflowDefinition> _definitions = new();
    private readonly ConcurrentDictionary<string, WorkflowExecution> _executions = new();

    public Task SaveDefinitionAsync(WorkflowDefinition definition, CancellationToken ct = default)
    {
        _definitions[definition.Id] = definition;
        return Task.CompletedTask;
    }

    public Task<WorkflowDefinition?> GetDefinitionAsync(string definitionId, CancellationToken ct = default)
    {
        _definitions.TryGetValue(definitionId, out var definition);
        return Task.FromResult(definition);
    }

    public Task<IReadOnlyList<WorkflowDefinition>> ListDefinitionsAsync(int limit = 50, CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<WorkflowDefinition>>(_definitions.Values.OrderByDescending(d => d.CreatedAt).Take(limit).ToList());
    }

    public Task DeleteDefinitionAsync(string definitionId, CancellationToken ct = default)
    {
        _definitions.TryRemove(definitionId, out _);
        return Task.CompletedTask;
    }

    public Task SaveExecutionAsync(WorkflowExecution execution, CancellationToken ct = default)
    {
        _executions[execution.Id] = execution;
        return Task.CompletedTask;
    }

    public Task<WorkflowExecution?> GetExecutionAsync(string executionId, CancellationToken ct = default)
    {
        _executions.TryGetValue(executionId, out var execution);
        return Task.FromResult(execution);
    }

    public Task<IReadOnlyList<WorkflowExecution>> ListExecutionsAsync(WorkflowExecutionStatus? status = null, int limit = 50, CancellationToken ct = default)
    {
        var query = _executions.Values.AsQueryable();
        if (status.HasValue)
        {
            query = query.Where(e => e.Status == status.Value);
        }
        return Task.FromResult<IReadOnlyList<WorkflowExecution>>(query.OrderByDescending(e => e.StartedAt).Take(limit).ToList());
    }

    public Task DeleteExecutionAsync(string executionId, CancellationToken ct = default)
    {
        _executions.TryRemove(executionId, out _);
        return Task.CompletedTask;
    }
}
