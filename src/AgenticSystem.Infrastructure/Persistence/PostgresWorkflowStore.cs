using System.Text.Json;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Infrastructure.Persistence;

public class PostgresWorkflowStore : IWorkflowStore
{
    private readonly IDbContextFactory<AgenticDbContext> _dbContextFactory;
    private readonly ILogger<PostgresWorkflowStore> _logger;

    public PostgresWorkflowStore(
        IDbContextFactory<AgenticDbContext> dbContextFactory,
        ILogger<PostgresWorkflowStore> logger)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    public async Task SaveDefinitionAsync(string tenantId, WorkflowDefinition definition, CancellationToken ct = default)
    {
        using var context = await _dbContextFactory.CreateDbContextAsync(ct);
        var entity = await context.WorkflowDefinitions.FindAsync(new object[] { definition.Id }, ct);

        if (entity == null)
        {
            entity = new WorkflowDefinitionEntity
            {
                Id = definition.Id,
                TenantId = tenantId,
                Name = definition.Name,
                Version = definition.Version,
                CreatedAt = definition.CreatedAt,
                DefinitionJson = JsonSerializer.Serialize(definition)
            };
            context.WorkflowDefinitions.Add(entity);
        }
        else
        {
            entity.Name = definition.Name;
            entity.Version = definition.Version;
            entity.DefinitionJson = JsonSerializer.Serialize(definition);
        }

        await context.SaveChangesAsync(ct);
    }

    public async Task<WorkflowDefinition?> GetDefinitionAsync(string tenantId, string definitionId, CancellationToken ct = default)
    {
        using var context = await _dbContextFactory.CreateDbContextAsync(ct);
        var entity = await context.WorkflowDefinitions.FirstOrDefaultAsync(w => w.Id == definitionId && w.TenantId == tenantId, ct);
        return entity == null ? null : JsonSerializer.Deserialize<WorkflowDefinition>(entity.DefinitionJson);
    }

    public async Task<IReadOnlyList<WorkflowDefinition>> ListDefinitionsAsync(string tenantId, int limit = 50, CancellationToken ct = default)
    {
        using var context = await _dbContextFactory.CreateDbContextAsync(ct);
        var entities = await context.WorkflowDefinitions
            .Where(w => w.TenantId == tenantId)
            .OrderByDescending(w => w.CreatedAt)
            .Take(limit)
            .ToListAsync(ct);

        return entities.Select(e => JsonSerializer.Deserialize<WorkflowDefinition>(e.DefinitionJson)!).ToList();
    }

    public async Task DeleteDefinitionAsync(string tenantId, string definitionId, CancellationToken ct = default)
    {
        using var context = await _dbContextFactory.CreateDbContextAsync(ct);
        var entity = await context.WorkflowDefinitions.FirstOrDefaultAsync(w => w.Id == definitionId && w.TenantId == tenantId, ct);
        if (entity != null)
        {
            context.WorkflowDefinitions.Remove(entity);
            await context.SaveChangesAsync(ct);
        }
    }

    public async Task SaveExecutionAsync(string tenantId, WorkflowExecution execution, CancellationToken ct = default)
    {
        using var context = await _dbContextFactory.CreateDbContextAsync(ct);
        
        var execEntity = await context.WorkflowExecutions.FindAsync(new object[] { execution.Id }, ct);
        if (execEntity == null)
        {
            execEntity = new WorkflowExecutionEntity
            {
                Id = execution.Id,
                TenantId = tenantId,
                WorkflowId = execution.WorkflowId,
                WorkflowName = execution.WorkflowName,
                Status = execution.Status.ToString(),
                InitiatedBy = execution.InitiatedBy,
                VariablesJson = JsonSerializer.Serialize(execution.Variables),
                StartedAt = execution.StartedAt,
                CompletedAt = execution.CompletedAt,
                ErrorMessage = execution.ErrorMessage
            };
            context.WorkflowExecutions.Add(execEntity);
        }
        else
        {
            execEntity.Status = execution.Status.ToString();
            execEntity.VariablesJson = JsonSerializer.Serialize(execution.Variables);
            execEntity.CompletedAt = execution.CompletedAt;
            execEntity.ErrorMessage = execution.ErrorMessage;
        }

        foreach (var stepExec in execution.StepExecutions)
        {
            var stepEntityId = $"{execution.Id}_{stepExec.StepId}";
            var stepEntity = await context.WorkflowStepExecutions.FindAsync(new object[] { stepEntityId }, ct);

            if (stepEntity == null)
            {
                stepEntity = new WorkflowStepExecutionEntity
                {
                    Id = stepEntityId,
                    TenantId = tenantId,
                    ExecutionId = execution.Id,
                    StepId = stepExec.StepId,
                    StepName = stepExec.StepName,
                    Status = stepExec.Status.ToString(),
                    OutputJson = JsonSerializer.Serialize(stepExec.Output),
                    ErrorMessage = stepExec.ErrorMessage,
                    RetryCount = stepExec.RetryCount,
                    CompensationExecuted = stepExec.CompensationExecuted,
                    StartedAt = stepExec.StartedAt,
                    CompletedAt = stepExec.CompletedAt
                };
                context.WorkflowStepExecutions.Add(stepEntity);
            }
            else
            {
                stepEntity.Status = stepExec.Status.ToString();
                stepEntity.OutputJson = JsonSerializer.Serialize(stepExec.Output);
                stepEntity.ErrorMessage = stepExec.ErrorMessage;
                stepEntity.RetryCount = stepExec.RetryCount;
                stepEntity.CompensationExecuted = stepExec.CompensationExecuted;
                stepEntity.CompletedAt = stepExec.CompletedAt;
            }
        }

        await context.SaveChangesAsync(ct);
    }

    public async Task<WorkflowExecution?> GetExecutionAsync(string tenantId, string executionId, CancellationToken ct = default)
    {
        using var context = await _dbContextFactory.CreateDbContextAsync(ct);
        var execEntity = await context.WorkflowExecutions.FirstOrDefaultAsync(e => e.Id == executionId && e.TenantId == tenantId, ct);
        if (execEntity == null) return null;

        var stepEntities = await context.WorkflowStepExecutions
            .Where(s => s.ExecutionId == executionId)
            .ToListAsync(ct);

        var execution = new WorkflowExecution
        {
            Id = execEntity.Id,
            WorkflowId = execEntity.WorkflowId,
            WorkflowName = execEntity.WorkflowName,
            Status = Enum.Parse<WorkflowExecutionStatus>(execEntity.Status),
            Variables = JsonSerializer.Deserialize<Dictionary<string, object>>(execEntity.VariablesJson) ?? new(),
            InitiatedBy = execEntity.InitiatedBy,
            ErrorMessage = execEntity.ErrorMessage,
            StartedAt = execEntity.StartedAt,
            CompletedAt = execEntity.CompletedAt
        };

        foreach (var stepEntity in stepEntities)
        {
            execution.StepExecutions.Add(new WorkflowStepExecution
            {
                StepId = stepEntity.StepId,
                StepName = stepEntity.StepName,
                Status = Enum.Parse<WorkflowExecutionStatus>(stepEntity.Status),
                Output = JsonSerializer.Deserialize<Dictionary<string, object>>(stepEntity.OutputJson) ?? new(),
                ErrorMessage = stepEntity.ErrorMessage,
                RetryCount = stepEntity.RetryCount,
                CompensationExecuted = stepEntity.CompensationExecuted,
                StartedAt = stepEntity.StartedAt,
                CompletedAt = stepEntity.CompletedAt
            });
        }

        return execution;
    }

    public async Task<IReadOnlyList<WorkflowExecution>> ListExecutionsAsync(string tenantId, WorkflowExecutionStatus? status = null, int limit = 50, CancellationToken ct = default)
    {
        using var context = await _dbContextFactory.CreateDbContextAsync(ct);
        var query = context.WorkflowExecutions.Where(e => e.TenantId == tenantId);

        if (status.HasValue)
        {
            var statusStr = status.Value.ToString();
            query = query.Where(e => e.Status == statusStr);
        }

        var entities = await query
            .OrderByDescending(e => e.StartedAt)
            .Take(limit)
            .ToListAsync(ct);

        return entities.Select(entity => new WorkflowExecution
        {
            Id = entity.Id,
            WorkflowId = entity.WorkflowId,
            WorkflowName = entity.WorkflowName,
            Status = Enum.Parse<WorkflowExecutionStatus>(entity.Status),
            InitiatedBy = entity.InitiatedBy,
            StartedAt = entity.StartedAt,
            CompletedAt = entity.CompletedAt
        }).ToList();
    }

    public async Task DeleteExecutionAsync(string tenantId, string executionId, CancellationToken ct = default)
    {
        using var context = await _dbContextFactory.CreateDbContextAsync(ct);
        var entity = await context.WorkflowExecutions.FirstOrDefaultAsync(e => e.Id == executionId && e.TenantId == tenantId, ct);
        if (entity != null)
        {
            var stepEntities = await context.WorkflowStepExecutions.Where(s => s.ExecutionId == executionId).ToListAsync(ct);
            context.WorkflowStepExecutions.RemoveRange(stepEntities);
            context.WorkflowExecutions.Remove(entity);
            await context.SaveChangesAsync(ct);
        }
    }
}
