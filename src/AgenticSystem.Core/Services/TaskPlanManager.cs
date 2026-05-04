using System.Collections.Concurrent;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Core.Services;

/// <summary>
/// Maturity Level 3 — Decomposição e execução persistente de tarefas multi-step.
/// </summary>
public class TaskPlanManager : ITaskPlanManager
{
    private readonly ConcurrentDictionary<string, TaskPlan> _plans = new();
    private readonly ILogger<TaskPlanManager> _logger;

    public TaskPlanManager(ILogger<TaskPlanManager> logger)
    {
        _logger = logger;
    }

    public Task<TaskPlan> CreatePlanAsync(string userId, string objective, List<TaskStep> steps)
    {
        var plan = new TaskPlan
        {
            UserId = userId,
            Title = objective,
            Description = objective,
            Steps = steps,
            Status = TaskPlanStatus.Created,
            CurrentStepIndex = 0
        };

        for (var i = 0; i < steps.Count; i++)
            steps[i].Index = i;

        _plans[plan.Id] = plan;
        _logger.LogInformation("Task plan created: {PlanId} with {StepCount} steps", plan.Id, steps.Count);

        return Task.FromResult(plan);
    }

    public Task<TaskPlan?> GetPlanAsync(string planId)
    {
        _plans.TryGetValue(planId, out var plan);
        return Task.FromResult(plan);
    }

    public Task<IEnumerable<TaskPlan>> GetActivePlansAsync(string userId)
    {
        var plans = _plans.Values
            .Where(p => p.UserId == userId && p.Status is TaskPlanStatus.Created or TaskPlanStatus.InProgress or TaskPlanStatus.Paused)
            .OrderByDescending(p => p.CreatedAt)
            .AsEnumerable();

        return Task.FromResult(plans);
    }

    public Task AdvanceStepAsync(string planId, string? result = null)
    {
        if (!_plans.TryGetValue(planId, out var plan))
            return Task.CompletedTask;

        if (plan.CurrentStepIndex >= plan.Steps.Count)
            return Task.CompletedTask;

        var currentStep = plan.Steps[plan.CurrentStepIndex];
        currentStep.Status = TaskStepStatus.Completed;
        currentStep.Result = result;
        currentStep.CompletedAt = DateTime.UtcNow;

        plan.CurrentStepIndex++;
        plan.Status = TaskPlanStatus.InProgress;

        if (plan.CurrentStepIndex < plan.Steps.Count)
        {
            var nextStep = plan.Steps[plan.CurrentStepIndex];
            nextStep.Status = TaskStepStatus.InProgress;
            nextStep.StartedAt = DateTime.UtcNow;
        }
        else
        {
            plan.Status = TaskPlanStatus.Completed;
            plan.CompletedAt = DateTime.UtcNow;
            _logger.LogInformation("Task plan {PlanId} completed", planId);
        }

        return Task.CompletedTask;
    }

    public Task FailStepAsync(string planId, string reason)
    {
        if (!_plans.TryGetValue(planId, out var plan))
            return Task.CompletedTask;

        if (plan.CurrentStepIndex >= plan.Steps.Count)
            return Task.CompletedTask;

        var currentStep = plan.Steps[plan.CurrentStepIndex];
        currentStep.Status = TaskStepStatus.Failed;
        currentStep.Result = reason;
        currentStep.CompletedAt = DateTime.UtcNow;
        plan.Status = TaskPlanStatus.Failed;

        _logger.LogWarning("Task plan {PlanId} step {StepIndex} failed: {Reason}", planId, plan.CurrentStepIndex, reason);
        return Task.CompletedTask;
    }

    public Task PausePlanAsync(string planId)
    {
        if (_plans.TryGetValue(planId, out var plan) && plan.Status == TaskPlanStatus.InProgress)
        {
            plan.Status = TaskPlanStatus.Paused;
            _logger.LogInformation("Task plan {PlanId} paused", planId);
        }

        return Task.CompletedTask;
    }

    public Task ResumePlanAsync(string planId)
    {
        if (_plans.TryGetValue(planId, out var plan) && plan.Status == TaskPlanStatus.Paused)
        {
            plan.Status = TaskPlanStatus.InProgress;
            if (plan.CurrentStepIndex < plan.Steps.Count)
            {
                plan.Steps[plan.CurrentStepIndex].Status = TaskStepStatus.InProgress;
                plan.Steps[plan.CurrentStepIndex].StartedAt = DateTime.UtcNow;
            }
            _logger.LogInformation("Task plan {PlanId} resumed", planId);
        }

        return Task.CompletedTask;
    }

    public Task CancelPlanAsync(string planId)
    {
        if (_plans.TryGetValue(planId, out var plan))
        {
            plan.Status = TaskPlanStatus.Cancelled;
            foreach (var step in plan.Steps.Where(s => s.Status is TaskStepStatus.Pending or TaskStepStatus.InProgress))
                step.Status = TaskStepStatus.Skipped;

            _logger.LogInformation("Task plan {PlanId} cancelled", planId);
        }

        return Task.CompletedTask;
    }
}
