using System.Text.Json;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Infrastructure.Persistence;

public sealed class PostgresScheduledTaskStore : IScheduledTaskStore
{
    private readonly IDbContextFactory<AgenticDbContext> _dbContextFactory;
    private readonly ILogger<PostgresScheduledTaskStore> _logger;

    public PostgresScheduledTaskStore(IDbContextFactory<AgenticDbContext> dbContextFactory, ILogger<PostgresScheduledTaskStore> logger)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    public async Task<ScheduledTask> SaveTaskAsync(ScheduledTask task, CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var entity = await db.ScheduledTasks.FirstOrDefaultAsync(item => item.Id == task.Id, ct);
        if (entity is null)
        {
            db.ScheduledTasks.Add(new ScheduledTaskEntity
            {
                Id = task.Id,
                Name = task.Name,
                Status = task.Status.ToString(),
                NextRunAt = task.NextRunAt,
                PayloadJson = JsonSerializer.Serialize(task),
                UpdatedAt = DateTime.UtcNow
            });
        }
        else
        {
            entity.Name = task.Name;
            entity.Status = task.Status.ToString();
            entity.NextRunAt = task.NextRunAt;
            entity.PayloadJson = JsonSerializer.Serialize(task);
            entity.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);
        return task;
    }

    public async Task<ScheduledTask?> GetTaskAsync(string taskId, CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var payload = await db.ScheduledTasks
            .AsNoTracking()
            .Where(item => item.Id == taskId)
            .Select(item => item.PayloadJson)
            .FirstOrDefaultAsync(ct);

        return payload is null ? null : JsonSerializer.Deserialize<ScheduledTask>(payload);
    }

    public async Task<IReadOnlyList<ScheduledTask>> GetAllTasksAsync(CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var payloads = await db.ScheduledTasks
            .AsNoTracking()
            .OrderByDescending(item => item.UpdatedAt)
            .Select(item => item.PayloadJson)
            .ToListAsync(ct);

        return payloads
            .Select(payload => JsonSerializer.Deserialize<ScheduledTask>(payload))
            .OfType<ScheduledTask>()
            .ToList();
    }

    public async Task DeleteTaskAsync(string taskId, CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var entity = await db.ScheduledTasks.FirstOrDefaultAsync(item => item.Id == taskId, ct);
        if (entity is null)
        {
            return;
        }

        db.ScheduledTasks.Remove(entity);
        await db.SaveChangesAsync(ct);
    }

    public async Task<TriggerRule> SaveRuleAsync(TriggerRule rule, CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var entity = await db.TriggerRules.FirstOrDefaultAsync(item => item.Id == rule.Id, ct);
        if (entity is null)
        {
            db.TriggerRules.Add(new TriggerRuleEntity
            {
                Id = rule.Id,
                Name = rule.Name,
                Enabled = rule.Enabled,
                PayloadJson = JsonSerializer.Serialize(rule),
                UpdatedAt = DateTime.UtcNow
            });
        }
        else
        {
            entity.Name = rule.Name;
            entity.Enabled = rule.Enabled;
            entity.PayloadJson = JsonSerializer.Serialize(rule);
            entity.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);
        return rule;
    }

    public async Task<TriggerRule?> GetRuleAsync(string ruleId, CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var payload = await db.TriggerRules
            .AsNoTracking()
            .Where(item => item.Id == ruleId)
            .Select(item => item.PayloadJson)
            .FirstOrDefaultAsync(ct);

        return payload is null ? null : JsonSerializer.Deserialize<TriggerRule>(payload);
    }

    public async Task<IReadOnlyList<TriggerRule>> GetAllRulesAsync(CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var payloads = await db.TriggerRules
            .AsNoTracking()
            .OrderByDescending(item => item.UpdatedAt)
            .Select(item => item.PayloadJson)
            .ToListAsync(ct);

        return payloads
            .Select(payload => JsonSerializer.Deserialize<TriggerRule>(payload))
            .OfType<TriggerRule>()
            .ToList();
    }

    public async Task DeleteRuleAsync(string ruleId, CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var entity = await db.TriggerRules.FirstOrDefaultAsync(item => item.Id == ruleId, ct);
        if (entity is null)
        {
            return;
        }

        db.TriggerRules.Remove(entity);
        await db.SaveChangesAsync(ct);
    }

    public async Task<TaskExecution> SaveExecutionAsync(TaskExecution execution, CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var entity = await db.ScheduledTaskExecutions.FirstOrDefaultAsync(item => item.ExecutionId == execution.ExecutionId, ct);
        if (entity is null)
        {
            db.ScheduledTaskExecutions.Add(new ScheduledTaskExecutionEntity
            {
                ExecutionId = execution.ExecutionId,
                TaskId = execution.TaskId,
                StartedAt = execution.StartedAt,
                CompletedAt = execution.CompletedAt,
                Success = execution.Success,
                PayloadJson = JsonSerializer.Serialize(execution)
            });
        }
        else
        {
            entity.TaskId = execution.TaskId;
            entity.StartedAt = execution.StartedAt;
            entity.CompletedAt = execution.CompletedAt;
            entity.Success = execution.Success;
            entity.PayloadJson = JsonSerializer.Serialize(execution);
        }

        await db.SaveChangesAsync(ct);
        _logger.LogDebug("Task execution persisted via EF Core: {ExecutionId}", execution.ExecutionId);
        return execution;
    }
}
