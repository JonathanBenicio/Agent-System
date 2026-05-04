using Microsoft.Extensions.Logging;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Services;

/// <summary>
/// ML21 — Gerencia tarefas agendadas com CRON ou TimeSpan.
/// Usa IScheduledTaskStore para persistência e ITriggerEngine para execução.
/// </summary>
public class ScheduledTaskManager : IScheduledTaskManager
{
    private readonly IScheduledTaskStore _store;
    private readonly ITriggerEngine _triggerEngine;
    private readonly ILogger<ScheduledTaskManager> _logger;

    public ScheduledTaskManager(
        IScheduledTaskStore store,
        ITriggerEngine triggerEngine,
        ILogger<ScheduledTaskManager> logger)
    {
        _store = store;
        _triggerEngine = triggerEngine;
        _logger = logger;
    }

    public async Task<ScheduledTask> RegisterAsync(string name, string cronExpression, TriggerRule? rule = null, CancellationToken ct = default)
    {
        var task = new ScheduledTask
        {
            Name = name,
            Schedule = cronExpression,
            Status = ScheduledTaskStatus.Active,
            AssociatedRule = rule,
            NextRunAt = CalculateNextRun(cronExpression)
        };

        if (rule != null)
        {
            await _triggerEngine.RegisterRuleAsync(rule, ct);
            task.AssociatedRule = rule;
        }

        await _store.SaveTaskAsync(task, ct);
        _logger.LogInformation("📅 Tarefa agendada registrada: {Name} [{Schedule}]", name, cronExpression);
        return task;
    }

    public async Task<ScheduledTask> RegisterAsync(string name, TimeSpan interval, TriggerRule? rule = null, CancellationToken ct = default)
    {
        var task = new ScheduledTask
        {
            Name = name,
            Schedule = $"interval:{interval.TotalSeconds}s",
            Interval = interval,
            Status = ScheduledTaskStatus.Active,
            AssociatedRule = rule,
            NextRunAt = DateTime.UtcNow.Add(interval)
        };

        if (rule != null)
        {
            await _triggerEngine.RegisterRuleAsync(rule, ct);
        }

        await _store.SaveTaskAsync(task, ct);
        _logger.LogInformation("📅 Tarefa agendada registrada: {Name} [intervalo: {Interval}]", name, interval);
        return task;
    }

    public async Task<ScheduledTask?> GetAsync(string taskId, CancellationToken ct = default)
    {
        return await _store.GetTaskAsync(taskId, ct);
    }

    public async Task<IReadOnlyList<ScheduledTask>> GetAllAsync(CancellationToken ct = default)
    {
        return await _store.GetAllTasksAsync(ct);
    }

    public async Task<IReadOnlyList<ScheduledTask>> GetActiveAsync(CancellationToken ct = default)
    {
        var all = await _store.GetAllTasksAsync(ct);
        return all.Where(t => t.Status == ScheduledTaskStatus.Active).ToList();
    }

    public async Task PauseAsync(string taskId, CancellationToken ct = default)
    {
        var task = await _store.GetTaskAsync(taskId, ct)
            ?? throw new InvalidOperationException($"Task {taskId} not found");

        task.Status = ScheduledTaskStatus.Paused;
        await _store.SaveTaskAsync(task, ct);
        _logger.LogInformation("⏸️ Tarefa pausada: {TaskId}", taskId);
    }

    public async Task ResumeAsync(string taskId, CancellationToken ct = default)
    {
        var task = await _store.GetTaskAsync(taskId, ct)
            ?? throw new InvalidOperationException($"Task {taskId} not found");

        task.Status = ScheduledTaskStatus.Active;
        task.NextRunAt = task.Interval.HasValue
            ? DateTime.UtcNow.Add(task.Interval.Value)
            : CalculateNextRun(task.Schedule);
        await _store.SaveTaskAsync(task, ct);
        _logger.LogInformation("▶️ Tarefa retomada: {TaskId}", taskId);
    }

    public async Task RemoveAsync(string taskId, CancellationToken ct = default)
    {
        var task = await _store.GetTaskAsync(taskId, ct);
        if (task?.AssociatedRule != null)
        {
            await _triggerEngine.RemoveRuleAsync(task.AssociatedRule.Id, ct);
        }
        await _store.DeleteTaskAsync(taskId, ct);
        _logger.LogInformation("🗑️ Tarefa removida: {TaskId}", taskId);
    }

    public async Task<TaskExecution> ExecuteAsync(string taskId, CancellationToken ct = default)
    {
        var task = await _store.GetTaskAsync(taskId, ct)
            ?? throw new InvalidOperationException($"Task {taskId} not found");

        var execution = new TaskExecution
        {
            TaskId = taskId,
            StartedAt = DateTime.UtcNow
        };

        try
        {
            if (task.AssociatedRule != null)
            {
                var result = await _triggerEngine.EvaluateAsync(task.AssociatedRule, ct);
                execution.Success = true;
                _logger.LogInformation("✅ Execução concluída: {TaskId} — condição: {Met}",
                    taskId, result.ConditionMet);
            }
            else
            {
                execution.Success = true;
            }
        }
        catch (Exception ex)
        {
            execution.Success = false;
            execution.ErrorMessage = ex.Message;
            task.FailedExecutions++;
            _logger.LogError(ex, "❌ Execução falhou: {TaskId}", taskId);
        }
        finally
        {
            execution.CompletedAt = DateTime.UtcNow;
            task.TotalExecutions++;
            task.LastRunAt = DateTime.UtcNow;

            if (task.Interval.HasValue)
                task.NextRunAt = DateTime.UtcNow.Add(task.Interval.Value);
            else
                task.NextRunAt = CalculateNextRun(task.Schedule);

            await _store.SaveTaskAsync(task, ct);
            await _store.SaveExecutionAsync(execution, ct);
        }

        return execution;
    }

    private static DateTime? CalculateNextRun(string cronExpression)
    {
        // Simplified CRON parser for v1 — supports basic intervals
        // Full CRON parsing can be added via NCronTab in future
        if (string.IsNullOrEmpty(cronExpression))
            return null;

        // Simple pattern: "*/5 * * * *" → every 5 minutes
        if (cronExpression.StartsWith("*/"))
        {
            var parts = cronExpression.Split(' ');
            if (parts.Length > 0 && int.TryParse(parts[0].Replace("*/", ""), out var minutes))
            {
                return DateTime.UtcNow.AddMinutes(minutes);
            }
        }

        // Default: 1 hour from now
        return DateTime.UtcNow.AddHours(1);
    }
}
