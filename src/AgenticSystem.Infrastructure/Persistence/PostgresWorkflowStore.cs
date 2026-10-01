using System.Text.Json;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace AgenticSystem.Infrastructure.Persistence;

public class PostgresWorkflowStore : IWorkflowStore
{
    private static readonly System.Threading.SemaphoreSlim _semaphore = new(1, 1);
    private readonly IDbContextFactory<AgenticDbContext> _dbContextFactory;
    private readonly ILogger<PostgresWorkflowStore> _logger;
    private readonly ITenantContextAccessor _tenantAccessor;

    public PostgresWorkflowStore(
        IDbContextFactory<AgenticDbContext> dbContextFactory,
        ILogger<PostgresWorkflowStore> logger,
        ITenantContextAccessor tenantAccessor)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
        _tenantAccessor = tenantAccessor;
    }

    public async Task SaveDefinitionAsync(string tenantId, WorkflowDefinition definition, CancellationToken ct = default)
    {
        TenantContextPolicy.RequireCurrentTenant(_tenantAccessor, tenantId);
        using var context = await _dbContextFactory.CreateDbContextAsync(ct);
        var entity = await context.WorkflowDefinitions
            .FirstOrDefaultAsync(item => item.Id == definition.Id && item.TenantId == tenantId, ct);

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
            if (!string.Equals(entity.TenantId, tenantId, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Workflow definition tenant ownership cannot be changed.");

            entity.Name = definition.Name;
            entity.Version = definition.Version;
            entity.DefinitionJson = JsonSerializer.Serialize(definition);
        }

        try
        {
            await context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            await using var ownershipContext = await _dbContextFactory.CreateDbContextAsync(ct);
            var existsForCurrentTenant = await ownershipContext.WorkflowDefinitions
                .AsNoTracking()
                .AnyAsync(item => item.Id == definition.Id && item.TenantId == tenantId, ct);
            if (!existsForCurrentTenant &&
                ex.InnerException is PostgresException
                {
                    SqlState: PostgresErrorCodes.UniqueViolation,
                    ConstraintName: "PK_workflow_definitions"
                })
                throw new InvalidOperationException("Workflow definition tenant ownership cannot be changed.");

            throw;
        }
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
        await _semaphore.WaitAsync(ct);
        try
        {
            using var context = await _dbContextFactory.CreateDbContextAsync(ct);
            await using var transaction = await context.Database.BeginTransactionAsync(ct);

            // Serialize lease validation and execution persistence against concurrent claims.
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE workflow_executions SET id = id WHERE id = {execution.Id} AND \"TenantId\" = {tenantId}",
                ct);

            var execEntity = await context.WorkflowExecutions.FindAsync(new object[] { execution.Id }, ct);
            if (execEntity == null)
            {
                execEntity = new WorkflowExecutionEntity
                {
                    Id = execution.Id,
                    TenantId = tenantId,
                    WorkflowId = execution.WorkflowId,
                    WorkflowName = execution.WorkflowName,
                    WorkflowDefinitionVersion = execution.WorkflowDefinitionVersion,
                    WorkflowDefinitionHash = execution.WorkflowDefinitionHash,
                    WorkflowDefinitionSnapshotJson = execution.WorkflowDefinitionSnapshotJson,
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
                if (!string.Equals(execEntity.TenantId, tenantId, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Workflow execution tenant ownership cannot be changed.");
                if (!string.Equals(execEntity.LeaseOwner, execution.LeaseOwner, StringComparison.Ordinal))
                    throw new WorkflowExecutionLeaseLostException(execution.Id);
                if (execEntity.LeaseOwner is not null && execEntity.LeaseExpiresAt <= DateTime.UtcNow)
                    throw new WorkflowExecutionLeaseLostException(execution.Id);
                if (execEntity.Status is nameof(WorkflowExecutionStatus.Completed)
                        or nameof(WorkflowExecutionStatus.Failed)
                        or nameof(WorkflowExecutionStatus.Cancelled)
                    && execution.Status == WorkflowExecutionStatus.Running)
                {
                    throw new WorkflowExecutionLeaseLostException(execution.Id);
                }

                if (string.IsNullOrWhiteSpace(execEntity.WorkflowDefinitionSnapshotJson)
                    && !string.IsNullOrWhiteSpace(execution.WorkflowDefinitionSnapshotJson))
                {
                    execEntity.WorkflowDefinitionVersion = execution.WorkflowDefinitionVersion;
                    execEntity.WorkflowDefinitionHash = execution.WorkflowDefinitionHash;
                    execEntity.WorkflowDefinitionSnapshotJson = execution.WorkflowDefinitionSnapshotJson;
                }
                execEntity.Status = execution.Status.ToString();
                execEntity.VariablesJson = JsonSerializer.Serialize(execution.Variables);
                execEntity.CompletedAt = execution.CompletedAt;
                execEntity.ErrorMessage = execution.ErrorMessage;
            }

            foreach (var stepExec in execution.StepExecutions)
            {
                var stepEntityId = $"{execution.Id}_{stepExec.StepId}";
                var stepEntity = await context.WorkflowStepExecutions.FindAsync(new object[] { stepEntityId }, ct);

                if (stepEntity is not null && !string.Equals(stepEntity.TenantId, tenantId, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Workflow step execution tenant ownership cannot be changed.");

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
                        CompletedAt = stepExec.CompletedAt,
                        WaitUntilUtc = stepExec.WaitUntilUtc
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
                    stepEntity.WaitUntilUtc = stepExec.WaitUntilUtc;
                }
            }

            await context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<WorkflowExecutionClaim?> ClaimNextExecutionAsync(
        string workerId,
        TimeSpan leaseDuration,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workerId);
        if (leaseDuration <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(leaseDuration));

        using var context = await _dbContextFactory.CreateDbContextAsync(ct);
        await context.Database.OpenConnectionAsync(ct);
        var connection = context.Database.GetDbConnection() as NpgsqlConnection
            ?? throw new InvalidOperationException("Workflow leases require PostgreSQL.");
        await using var transaction = await connection.BeginTransactionAsync(ct);

        string? executionId = null;
        string? tenantId = null;
        string? previousStatus = null;
        await using (var select = new NpgsqlCommand(
            "SELECT id, \"TenantId\", status FROM workflow_executions " +
            "WHERE status IN ('Pending', 'Running') AND (lease_expires_at IS NULL OR lease_expires_at <= @now) " +
            "AND NOT EXISTS (SELECT 1 FROM workflow_step_executions s WHERE s.execution_id = workflow_executions.id AND s.\"TenantId\" = workflow_executions.\"TenantId\" " +
            "AND s.status = 'Pending' AND s.wait_until_utc IS NOT NULL AND s.wait_until_utc > @now) " +
            "ORDER BY started_at, id LIMIT 1 FOR UPDATE SKIP LOCKED",
            connection,
            transaction))
        {
            select.Parameters.AddWithValue("now", DateTime.UtcNow);
            await using var reader = await select.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                executionId = reader.GetString(0);
                tenantId = reader.GetString(1);
                previousStatus = reader.GetString(2);
            }
        }

        if (executionId is null || tenantId is null || previousStatus is null)
        {
            await transaction.RollbackAsync(ct);
            return null;
        }

        var now = DateTime.UtcNow;
        var expiresAt = now.Add(leaseDuration);
        await using (var update = new NpgsqlCommand(
            "UPDATE workflow_executions SET status = 'Running', lease_owner = @worker, lease_expires_at = @expires " +
            "WHERE id = @id AND \"TenantId\" = @tenant",
            connection,
            transaction))
        {
            update.Parameters.AddWithValue("worker", workerId);
            update.Parameters.AddWithValue("expires", expiresAt);
            update.Parameters.AddWithValue("id", executionId);
            update.Parameters.AddWithValue("tenant", tenantId);
            if (await update.ExecuteNonQueryAsync(ct) != 1)
                throw new InvalidOperationException("A locked workflow execution disappeared before it could be claimed.");
        }

        if (previousStatus == nameof(WorkflowExecutionStatus.Running))
        {
            await using var reset = new NpgsqlCommand(
                "UPDATE workflow_step_executions SET status = 'Pending', completed_at = NULL " +
                "WHERE execution_id = @id AND \"TenantId\" = @tenant AND status = 'Running'",
                connection,
                transaction);
            reset.Parameters.AddWithValue("id", executionId);
            reset.Parameters.AddWithValue("tenant", tenantId);
            await reset.ExecuteNonQueryAsync(ct);
        }

        await transaction.CommitAsync(ct);
        return new WorkflowExecutionClaim(tenantId, executionId, workerId);
    }

    public async Task<bool> RenewExecutionLeaseAsync(
        WorkflowExecutionClaim claim,
        TimeSpan leaseDuration,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(claim);
        if (leaseDuration <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(leaseDuration));

        using var context = await _dbContextFactory.CreateDbContextAsync(ct);
        var connection = context.Database.GetDbConnection() as NpgsqlConnection
            ?? throw new InvalidOperationException("Workflow leases require PostgreSQL.");
        await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand(
            "UPDATE workflow_executions SET lease_expires_at = @expires " +
            "WHERE id = @id AND \"TenantId\" = @tenant AND lease_owner = @worker " +
            "AND status = 'Running' AND lease_expires_at > @now",
            connection);
        command.Parameters.AddWithValue("expires", DateTime.UtcNow.Add(leaseDuration));
        command.Parameters.AddWithValue("id", claim.ExecutionId);
        command.Parameters.AddWithValue("tenant", claim.TenantId);
        command.Parameters.AddWithValue("worker", claim.WorkerId);
        command.Parameters.AddWithValue("now", DateTime.UtcNow);
        return await command.ExecuteNonQueryAsync(ct) == 1;
    }

    public async Task ReleaseExecutionLeaseAsync(WorkflowExecutionClaim claim, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(claim);
        using var context = await _dbContextFactory.CreateDbContextAsync(ct);
        var connection = context.Database.GetDbConnection() as NpgsqlConnection
            ?? throw new InvalidOperationException("Workflow leases require PostgreSQL.");
        await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand(
            "UPDATE workflow_executions SET lease_owner = NULL, lease_expires_at = NULL " +
            "WHERE id = @id AND \"TenantId\" = @tenant AND lease_owner = @worker",
            connection);
        command.Parameters.AddWithValue("id", claim.ExecutionId);
        command.Parameters.AddWithValue("tenant", claim.TenantId);
        command.Parameters.AddWithValue("worker", claim.WorkerId);
        await command.ExecuteNonQueryAsync(ct);
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
            TenantId = execEntity.TenantId,
            WorkflowId = execEntity.WorkflowId,
            WorkflowName = execEntity.WorkflowName,
            WorkflowDefinitionVersion = execEntity.WorkflowDefinitionVersion,
            WorkflowDefinitionHash = execEntity.WorkflowDefinitionHash ?? string.Empty,
            WorkflowDefinitionSnapshotJson = execEntity.WorkflowDefinitionSnapshotJson ?? string.Empty,
            LeaseOwner = execEntity.LeaseOwner,
            LeaseExpiresAt = execEntity.LeaseExpiresAt,
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
                CompletedAt = stepEntity.CompletedAt,
                WaitUntilUtc = stepEntity.WaitUntilUtc
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
            TenantId = entity.TenantId,
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
