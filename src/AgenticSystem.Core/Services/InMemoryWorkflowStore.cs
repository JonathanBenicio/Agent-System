using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Services;

public class InMemoryWorkflowStore : IWorkflowStore
{
    private readonly object _sync = new();
    private readonly List<StoredWorkflowDefinition> _definitions = new();
    private readonly List<WorkflowExecution> _executions = new();

    public Task SaveDefinitionAsync(string tenantId, WorkflowDefinition definition, CancellationToken ct = default)
    {
        lock (_sync)
        {
            var existing = _definitions.FirstOrDefault(item => item.Definition.Id == definition.Id);
            if (existing is not null && !string.Equals(existing.TenantId, tenantId, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Workflow definition tenant ownership cannot be changed.");
            if (existing != null) _definitions.Remove(existing);
            _definitions.Add(new StoredWorkflowDefinition(tenantId, definition));
        }
        return Task.CompletedTask;
    }

    public Task<WorkflowDefinition?> GetDefinitionAsync(string tenantId, string definitionId, CancellationToken ct = default)
    {
        lock (_sync)
            return Task.FromResult(_definitions.FirstOrDefault(item =>
                item.TenantId == tenantId && item.Definition.Id == definitionId)?.Definition);
    }

    public Task<IReadOnlyList<WorkflowDefinition>> ListDefinitionsAsync(string tenantId, int limit = 50, CancellationToken ct = default)
    {
        lock (_sync)
            return Task.FromResult<IReadOnlyList<WorkflowDefinition>>(_definitions
                .Where(item => item.TenantId == tenantId)
                .OrderByDescending(item => item.Definition.CreatedAt)
                .Take(limit)
                .Select(item => item.Definition)
                .ToList());
    }

    public Task DeleteDefinitionAsync(string tenantId, string definitionId, CancellationToken ct = default)
    {
        lock (_sync)
        {
            var existing = _definitions.FirstOrDefault(item =>
                item.TenantId == tenantId && item.Definition.Id == definitionId);
            if (existing != null) _definitions.Remove(existing);
        }
        return Task.CompletedTask;
    }

    public Task SaveExecutionAsync(string tenantId, WorkflowExecution execution, CancellationToken ct = default)
    {
        lock (_sync)
        {
            var existing = _executions.FirstOrDefault(e => e.Id == execution.Id);
            if (existing != null)
            {
                if (!string.Equals(existing.TenantId, tenantId, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Workflow execution tenant ownership cannot be changed.");
                if (!string.Equals(existing.LeaseOwner, execution.LeaseOwner, StringComparison.Ordinal))
                    throw new WorkflowExecutionLeaseLostException(execution.Id);
                if (existing.LeaseOwner is not null && existing.LeaseExpiresAt <= DateTime.UtcNow)
                    throw new WorkflowExecutionLeaseLostException(execution.Id);
                if (IsTerminal(existing.Status) && execution.Status == WorkflowExecutionStatus.Running)
                    throw new WorkflowExecutionLeaseLostException(execution.Id);
                _executions.Remove(existing);
            }
            execution.TenantId = tenantId;
            _executions.Add(Clone(execution));
        }
        return Task.CompletedTask;
    }

    public Task<WorkflowExecutionClaim?> ClaimNextExecutionAsync(string workerId, TimeSpan leaseDuration, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workerId);
        if (leaseDuration <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(leaseDuration));
        ct.ThrowIfCancellationRequested();

        lock (_sync)
        {
            var now = DateTime.UtcNow;
            var execution = _executions
                .Where(item => (item.Status is WorkflowExecutionStatus.Pending or WorkflowExecutionStatus.Running)
                    && (item.LeaseExpiresAt is null || item.LeaseExpiresAt <= now))
                .Where(item => !item.StepExecutions.Any(step => step.Status == WorkflowExecutionStatus.Pending
                    && step.WaitUntilUtc.HasValue && step.WaitUntilUtc.Value > now))
                .OrderBy(item => item.StartedAt)
                .FirstOrDefault();
            if (execution is null) return Task.FromResult<WorkflowExecutionClaim?>(null);

            foreach (var step in execution.StepExecutions.Where(step => step.Status == WorkflowExecutionStatus.Running))
            {
                step.Status = WorkflowExecutionStatus.Pending;
                step.CompletedAt = null;
            }

            execution.Status = WorkflowExecutionStatus.Running;
            execution.LeaseOwner = workerId;
            execution.LeaseExpiresAt = now.Add(leaseDuration);
            return Task.FromResult<WorkflowExecutionClaim?>(
                new WorkflowExecutionClaim(execution.TenantId, execution.Id, workerId));
        }
    }

    public Task<bool> RenewExecutionLeaseAsync(WorkflowExecutionClaim claim, TimeSpan leaseDuration, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        lock (_sync)
        {
            var execution = _executions.FirstOrDefault(item => item.Id == claim.ExecutionId
                && item.TenantId == claim.TenantId
                && item.LeaseOwner == claim.WorkerId
                && item.Status == WorkflowExecutionStatus.Running
                && item.LeaseExpiresAt > DateTime.UtcNow);
            if (execution is null) return Task.FromResult(false);
            execution.LeaseExpiresAt = DateTime.UtcNow.Add(leaseDuration);
            return Task.FromResult(true);
        }
    }

    public Task ReleaseExecutionLeaseAsync(WorkflowExecutionClaim claim, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        lock (_sync)
        {
            var execution = _executions.FirstOrDefault(item => item.Id == claim.ExecutionId
                && item.TenantId == claim.TenantId
                && item.LeaseOwner == claim.WorkerId);
            if (execution is not null)
            {
                execution.LeaseOwner = null;
                execution.LeaseExpiresAt = null;
                if (execution.Status == WorkflowExecutionStatus.Running)
                    execution.Status = WorkflowExecutionStatus.Pending;
            }
        }

        return Task.CompletedTask;
    }

    public Task<WorkflowExecution?> GetExecutionAsync(string tenantId, string executionId, CancellationToken ct = default)
    {
        lock (_sync)
            return Task.FromResult(_executions.FirstOrDefault(e => e.Id == executionId && e.TenantId == tenantId) is { } execution
                ? Clone(execution) : null);
    }

    public Task<IReadOnlyList<WorkflowExecution>> ListExecutionsAsync(string tenantId, WorkflowExecutionStatus? status = null, int limit = 50, CancellationToken ct = default)
    {
        lock (_sync)
        {
            var query = _executions.Where(e => e.TenantId == tenantId);
            if (status.HasValue) query = query.Where(e => e.Status == status.Value);
            return Task.FromResult<IReadOnlyList<WorkflowExecution>>(query.OrderByDescending(e => e.StartedAt).Take(limit).Select(Clone).ToList());
        }
    }

    public Task DeleteExecutionAsync(string tenantId, string executionId, CancellationToken ct = default)
    {
        lock (_sync)
        {
            var existing = _executions.FirstOrDefault(e => e.Id == executionId && e.TenantId == tenantId);
            if (existing != null) _executions.Remove(existing);
        }
        return Task.CompletedTask;
    }

    private static bool IsTerminal(WorkflowExecutionStatus status) =>
        status is WorkflowExecutionStatus.Completed or WorkflowExecutionStatus.Failed or WorkflowExecutionStatus.Cancelled;

    private static WorkflowExecution Clone(WorkflowExecution execution)
    {
        return new WorkflowExecution
        {
            Id = execution.Id,
            TenantId = execution.TenantId,
            WorkflowId = execution.WorkflowId,
            WorkflowName = execution.WorkflowName,
            WorkflowDefinitionVersion = execution.WorkflowDefinitionVersion,
            WorkflowDefinitionHash = execution.WorkflowDefinitionHash,
            WorkflowDefinitionSnapshotJson = execution.WorkflowDefinitionSnapshotJson,
            LeaseOwner = execution.LeaseOwner,
            LeaseExpiresAt = execution.LeaseExpiresAt,
            Status = execution.Status,
            StepExecutions = execution.StepExecutions.Select(step => new WorkflowStepExecution
            {
                StepId = step.StepId,
                StepName = step.StepName,
                Status = step.Status,
                Output = new Dictionary<string, object>(step.Output),
                ErrorMessage = step.ErrorMessage,
                RetryCount = step.RetryCount,
                CompensationExecuted = step.CompensationExecuted,
                StartedAt = step.StartedAt,
                CompletedAt = step.CompletedAt,
                WaitUntilUtc = step.WaitUntilUtc
            }).ToList(),
            Variables = new Dictionary<string, object>(execution.Variables),
            InitiatedBy = execution.InitiatedBy,
            ErrorMessage = execution.ErrorMessage,
            StartedAt = execution.StartedAt,
            CompletedAt = execution.CompletedAt
        };
    }

    private sealed record StoredWorkflowDefinition(string TenantId, WorkflowDefinition Definition);
}
