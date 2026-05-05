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

    public async Task<ScheduledTask> RegisterAsync(
        string name,
        string cronExpression,
        TriggerRule? rule = null,
        int maxRetryAttempts = 3,
        CancellationToken ct = default)
    {
        var task = new ScheduledTask
        {
            Name = name,
            Schedule = cronExpression,
            Status = ScheduledTaskStatus.Active,
            MaxRetryAttempts = Math.Max(1, maxRetryAttempts),
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

    public async Task<ScheduledTask> RegisterAsync(
        string name,
        TimeSpan interval,
        TriggerRule? rule = null,
        int maxRetryAttempts = 3,
        CancellationToken ct = default)
    {
        var task = new ScheduledTask
        {
            Name = name,
            Schedule = $"interval:{interval.TotalSeconds}s",
            Interval = interval,
            Status = ScheduledTaskStatus.Active,
            MaxRetryAttempts = Math.Max(1, maxRetryAttempts),
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

    public async Task LinkTasksAsync(string predecessorTaskId, string successorTaskId, CancellationToken ct = default)
    {
        if (string.Equals(predecessorTaskId, successorTaskId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("A task cannot depend on itself.");
        }

        var allTasks = await _store.GetAllTasksAsync(ct);
        var taskMap = allTasks.ToDictionary(task => task.Id, StringComparer.OrdinalIgnoreCase);

        if (!taskMap.TryGetValue(predecessorTaskId, out var predecessor))
        {
            throw new InvalidOperationException($"Task {predecessorTaskId} not found");
        }

        if (!taskMap.TryGetValue(successorTaskId, out var successor))
        {
            throw new InvalidOperationException($"Task {successorTaskId} not found");
        }

        if (WouldIntroduceCycle(predecessorTaskId, successorTaskId, taskMap))
        {
            throw new InvalidOperationException(
                $"Linking task {predecessorTaskId} -> {successorTaskId} would introduce a cycle in the DAG.");
        }

        if (!predecessor.ContinuationTaskIds.Contains(successorTaskId, StringComparer.OrdinalIgnoreCase))
        {
            predecessor.ContinuationTaskIds.Add(successorTaskId);
        }

        if (!successor.DependencyTaskIds.Contains(predecessorTaskId, StringComparer.OrdinalIgnoreCase))
        {
            successor.DependencyTaskIds.Add(predecessorTaskId);
        }

        successor.NextRunAt = AreDependenciesSatisfied(successor, taskMap)
            ? DateTime.UtcNow
            : null;

        await _store.SaveTaskAsync(predecessor, ct);
        await _store.SaveTaskAsync(successor, ct);

        _logger.LogInformation(
            "🔗 Task link registered: {PredecessorTaskId} -> {SuccessorTaskId}",
            predecessorTaskId,
            successorTaskId);
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
        task.ConsecutiveFailures = 0;
        task.DeadLetterReason = null;

        if (task.DependencyTaskIds.Count > 0)
        {
            var allTasks = await _store.GetAllTasksAsync(ct);
            var taskMap = allTasks.ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);
            task.NextRunAt = AreDependenciesSatisfied(task, taskMap)
                ? DateTime.UtcNow
                : null;
        }
        else
        {
            task.NextRunAt = task.Interval.HasValue
                ? DateTime.UtcNow.Add(task.Interval.Value)
                : CalculateNextRun(task.Schedule);
        }

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

        var allTasks = await _store.GetAllTasksAsync(ct);
        foreach (var other in allTasks.Where(other => other.Id != taskId))
        {
            var changed = other.DependencyTaskIds.RemoveAll(id => string.Equals(id, taskId, StringComparison.OrdinalIgnoreCase)) > 0;
            changed |= other.ContinuationTaskIds.RemoveAll(id => string.Equals(id, taskId, StringComparison.OrdinalIgnoreCase)) > 0;

            if (changed)
            {
                await _store.SaveTaskAsync(other, ct);
            }
        }

        await _store.DeleteTaskAsync(taskId, ct);
        _logger.LogInformation("🗑️ Tarefa removida: {TaskId}", taskId);
    }

    public async Task<TaskExecution> ExecuteAsync(string taskId, CancellationToken ct = default)
    {
        var task = await _store.GetTaskAsync(taskId, ct)
            ?? throw new InvalidOperationException($"Task {taskId} not found");

        if (task.DependencyTaskIds.Count > 0)
        {
            var allTasks = await _store.GetAllTasksAsync(ct);
            var taskMap = allTasks.ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);
            if (!AreDependenciesSatisfied(task, taskMap))
            {
                throw new InvalidOperationException($"Task {taskId} has unresolved dependencies and cannot execute yet.");
            }
        }

        var execution = new TaskExecution
        {
            TaskId = taskId,
            StartedAt = DateTime.UtcNow,
            AttemptNumber = task.ConsecutiveFailures + 1
        };

        var completedAt = DateTime.UtcNow;

        try
        {
            if (task.AssociatedRule != null)
            {
                var result = await _triggerEngine.EvaluateAsync(task.AssociatedRule, ct);
                execution.Success = true;
                task.Status = ScheduledTaskStatus.Active;
                task.ConsecutiveFailures = 0;
                task.DeadLetterReason = null;
                _logger.LogInformation("✅ Execução concluída: {TaskId} — condição: {Met}",
                    taskId, result.ConditionMet);
            }
            else
            {
                execution.Success = true;
                task.Status = ScheduledTaskStatus.Active;
                task.ConsecutiveFailures = 0;
                task.DeadLetterReason = null;
            }
        }
        catch (Exception ex)
        {
            execution.Success = false;
            execution.ErrorMessage = ex.Message;
            task.FailedExecutions++;
            task.ConsecutiveFailures++;
            task.LastFailedAt = DateTime.UtcNow;

            if (task.ConsecutiveFailures >= task.MaxRetryAttempts)
            {
                execution.DeadLettered = true;
                task.Status = ScheduledTaskStatus.Failed;
                task.DeadLetterReason = ex.Message;
                task.NextRunAt = null;
            }
            else
            {
                task.Status = ScheduledTaskStatus.Active;
                task.NextRunAt = DateTime.UtcNow.Add(CalculateRetryDelay(task.ConsecutiveFailures));
            }

            _logger.LogError(ex, "❌ Execução falhou: {TaskId}", taskId);
        }
        finally
        {
            completedAt = DateTime.UtcNow;
            execution.CompletedAt = completedAt;
            task.TotalExecutions++;
            task.LastRunAt = completedAt;

            if (execution.Success)
            {
                task.NextRunAt = task.DependencyTaskIds.Count > 0
                    ? null
                    : task.Interval.HasValue
                        ? completedAt.Add(task.Interval.Value)
                        : CalculateNextRun(task.Schedule);
            }

            await _store.SaveTaskAsync(task, ct);
            await _store.SaveExecutionAsync(execution, ct);

            if (execution.Success)
            {
                await ActivateContinuationTasksAsync(task, completedAt, ct);
            }
        }

        return execution;
    }

    private async Task ActivateContinuationTasksAsync(ScheduledTask completedTask, DateTime completedAt, CancellationToken ct)
    {
        if (completedTask.ContinuationTaskIds.Count == 0)
        {
            return;
        }

        var allTasks = await _store.GetAllTasksAsync(ct);
        var taskMap = allTasks.ToDictionary(task => task.Id, StringComparer.OrdinalIgnoreCase);

        foreach (var continuationTaskId in completedTask.ContinuationTaskIds)
        {
            if (!taskMap.TryGetValue(continuationTaskId, out var continuationTask))
            {
                continue;
            }

            if (continuationTask.Status != ScheduledTaskStatus.Active)
            {
                continue;
            }

            if (!AreDependenciesSatisfied(continuationTask, taskMap))
            {
                continue;
            }

            continuationTask.NextRunAt = completedAt;
            await _store.SaveTaskAsync(continuationTask, ct);

            _logger.LogInformation(
                "⛓️ Continuation task activated: {ContinuationTaskId} after {CompletedTaskId}",
                continuationTaskId,
                completedTask.Id);
        }
    }

    private static bool AreDependenciesSatisfied(
        ScheduledTask task,
        IReadOnlyDictionary<string, ScheduledTask> taskMap)
    {
        foreach (var dependencyTaskId in task.DependencyTaskIds)
        {
            if (!taskMap.TryGetValue(dependencyTaskId, out var dependencyTask))
            {
                return false;
            }

            if (dependencyTask.LastRunAt is null || dependencyTask.Status == ScheduledTaskStatus.Failed || dependencyTask.ConsecutiveFailures > 0)
            {
                return false;
            }
        }

        return true;
    }

    private static bool WouldIntroduceCycle(
        string predecessorTaskId,
        string successorTaskId,
        IReadOnlyDictionary<string, ScheduledTask> taskMap)
    {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var stack = new Stack<string>();
        stack.Push(successorTaskId);

        while (stack.Count > 0)
        {
            var currentTaskId = stack.Pop();
            if (!visited.Add(currentTaskId))
            {
                continue;
            }

            if (string.Equals(currentTaskId, predecessorTaskId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!taskMap.TryGetValue(currentTaskId, out var currentTask))
            {
                continue;
            }

            foreach (var continuationTaskId in currentTask.ContinuationTaskIds)
            {
                stack.Push(continuationTaskId);
            }
        }

        return false;
    }

    private static TimeSpan CalculateRetryDelay(int consecutiveFailures)
    {
        var seconds = Math.Min(300, 30 * Math.Pow(2, Math.Max(0, consecutiveFailures - 1)));
        return TimeSpan.FromSeconds(seconds);
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
