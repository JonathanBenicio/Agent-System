using System.Collections.Concurrent;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Services;

/// <summary>
/// ML21 — Store in-memory para tarefas agendadas e regras de trigger.
/// </summary>
public class InMemoryScheduledTaskStore : IScheduledTaskStore
{
    private readonly ConcurrentDictionary<string, ScheduledTask> _tasks = new();
    private readonly ConcurrentDictionary<string, TriggerRule> _rules = new();
    private readonly ConcurrentDictionary<string, TaskExecution> _executions = new();

    public Task<ScheduledTask> SaveTaskAsync(ScheduledTask task, CancellationToken ct = default)
    {
        _tasks[task.Id] = task;
        return Task.FromResult(task);
    }

    public Task<ScheduledTask?> GetTaskAsync(string taskId, CancellationToken ct = default)
    {
        _tasks.TryGetValue(taskId, out var task);
        return Task.FromResult(task);
    }

    public Task<IReadOnlyList<ScheduledTask>> GetAllTasksAsync(CancellationToken ct = default)
    {
        IReadOnlyList<ScheduledTask> result = _tasks.Values.ToList();
        return Task.FromResult(result);
    }

    public Task DeleteTaskAsync(string taskId, CancellationToken ct = default)
    {
        _tasks.TryRemove(taskId, out _);
        return Task.CompletedTask;
    }

    public Task<TriggerRule> SaveRuleAsync(TriggerRule rule, CancellationToken ct = default)
    {
        _rules[rule.Id] = rule;
        return Task.FromResult(rule);
    }

    public Task<TriggerRule?> GetRuleAsync(string ruleId, CancellationToken ct = default)
    {
        _rules.TryGetValue(ruleId, out var rule);
        return Task.FromResult(rule);
    }

    public Task<IReadOnlyList<TriggerRule>> GetAllRulesAsync(CancellationToken ct = default)
    {
        IReadOnlyList<TriggerRule> result = _rules.Values.ToList();
        return Task.FromResult(result);
    }

    public Task DeleteRuleAsync(string ruleId, CancellationToken ct = default)
    {
        _rules.TryRemove(ruleId, out _);
        return Task.CompletedTask;
    }

    public Task<TaskExecution> SaveExecutionAsync(TaskExecution execution, CancellationToken ct = default)
    {
        _executions[execution.ExecutionId] = execution;
        return Task.FromResult(execution);
    }
}
