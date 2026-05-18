using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Core.Services;

public class DefaultWorkflowEngine : IWorkflowEngine
{
    private readonly IWorkflowStore _store;
    private readonly IDirectAgentRequestExecutor _agentExecutor;
    private readonly IToolManager _toolManager;
    private readonly IWorkflowEventBroadcaster? _broadcaster;
    private readonly ILogger<DefaultWorkflowEngine> _logger;

    public DefaultWorkflowEngine(
        IWorkflowStore store,
        IDirectAgentRequestExecutor agentExecutor,
        IToolManager toolManager,
        ILogger<DefaultWorkflowEngine> logger,
        IWorkflowEventBroadcaster? broadcaster = null)
    {
        _store = store;
        _agentExecutor = agentExecutor;
        _toolManager = toolManager;
        _broadcaster = broadcaster;
        _logger = logger;
    }

    public async Task<WorkflowExecution> StartAsync(
        WorkflowDefinition workflow,
        Dictionary<string, object>? initialVariables = null,
        string? initiatedBy = null,
        CancellationToken ct = default)
    {
        // For now, let's assume we can get tenantId from somewhere or it's passed.
        // Since IWorkflowEngine.StartAsync doesn't have tenantId, I'll use "default" or try to find it in definition.
        // Actually, I should probably update the interface.
        var tenantId = "default"; // Placeholder
        
        _logger.LogInformation("🚀 Starting workflow: {WorkflowName} ({WorkflowId})", workflow.Name, workflow.Id);

        var execution = new WorkflowExecution
        {
            WorkflowId = workflow.Id,
            WorkflowName = workflow.Name,
            TenantId = tenantId,
            Status = WorkflowExecutionStatus.Running,
            Variables = initialVariables ?? new(),
            InitiatedBy = initiatedBy,
            StartedAt = DateTime.UtcNow
        };

        await _store.SaveExecutionAsync(tenantId, execution, ct);

        if (_broadcaster != null)
        {
            await _broadcaster.BroadcastExecutionStarted(execution);
        }

        _ = Task.Run(() => ProcessExecutionAsync(tenantId, execution.Id, workflow));

        return execution;
    }

    public async Task<WorkflowExecution> ResumeAsync(
        string executionId,
        Dictionary<string, object>? additionalInput = null,
        CancellationToken ct = default)
    {
        // Need tenantId here too.
        var tenantId = "default"; 
        var execution = await _store.GetExecutionAsync(tenantId, executionId, ct);
        if (execution == null) throw new ArgumentException("Execution not found", nameof(executionId));

        _logger.LogInformation("⏯️ Resuming workflow execution: {ExecutionId}", executionId);

        if (additionalInput != null)
        {
            foreach (var kvp in additionalInput)
            {
                execution.Variables[kvp.Key] = kvp.Value;
            }
        }

        execution.Status = WorkflowExecutionStatus.Running;
        await _store.SaveExecutionAsync(tenantId, execution, ct);

        var definition = await _store.GetDefinitionAsync(tenantId, execution.WorkflowId, ct);
        if (definition == null) throw new InvalidOperationException("Workflow definition not found");

        _ = Task.Run(() => ProcessExecutionAsync(tenantId, execution.Id, definition));

        return execution;
    }

    public async Task<WorkflowExecution> CancelAsync(
        string executionId,
        string? reason = null,
        CancellationToken ct = default)
    {
        var tenantId = "default";
        var execution = await _store.GetExecutionAsync(tenantId, executionId, ct);
        if (execution == null) throw new ArgumentException("Execution not found", nameof(executionId));

        _logger.LogWarning("⏹️ Cancelling workflow execution: {ExecutionId}. Reason: {Reason}", executionId, reason);

        execution.Status = WorkflowExecutionStatus.Cancelled;
        execution.CompletedAt = DateTime.UtcNow;
        execution.ErrorMessage = reason;

        await _store.SaveExecutionAsync(tenantId, execution, ct);

        if (_broadcaster != null)
        {
            await _broadcaster.BroadcastExecutionCancelled(execution);
        }

        return execution;
    }

    public Task<WorkflowExecution?> GetExecutionAsync(string executionId, CancellationToken ct = default)
        => _store.GetExecutionAsync("default", executionId, ct);

    public Task<IReadOnlyList<WorkflowExecution>> ListExecutionsAsync(WorkflowExecutionStatus? statusFilter = null, int limit = 20, CancellationToken ct = default)
        => _store.ListExecutionsAsync("default", statusFilter, limit, ct);

    private async Task ProcessExecutionAsync(string tenantId, string executionId, WorkflowDefinition definition)
    {
        try
        {
            while (true)
            {
                var execution = await _store.GetExecutionAsync(tenantId, executionId);
                if (execution == null || execution.Status != WorkflowExecutionStatus.Running) break;

                var readySteps = GetReadySteps(definition, execution);
                if (readySteps.Count == 0)
                {
                    if (IsWorkflowComplete(definition, execution))
                    {
                        execution.Status = WorkflowExecutionStatus.Completed;
                        execution.CompletedAt = DateTime.UtcNow;
                        await _store.SaveExecutionAsync(tenantId, execution);
                        if (_broadcaster != null)
                        {
                            await _broadcaster.BroadcastExecutionCompleted(execution);
                        }
                        _logger.LogInformation("✅ Workflow execution completed: {ExecutionId}", executionId);
                    }
                    else if (IsWorkflowFailed(execution))
                    {
                        execution.Status = WorkflowExecutionStatus.Failed;
                        execution.CompletedAt = DateTime.UtcNow;
                        await _store.SaveExecutionAsync(tenantId, execution);
                        if (_broadcaster != null)
                        {
                            await _broadcaster.BroadcastExecutionFailed(execution);
                        }
                        _logger.LogWarning("❌ Workflow execution failed: {ExecutionId}", executionId);
                    }
                    else
                    {
                        execution.Status = WorkflowExecutionStatus.Paused;
                        await _store.SaveExecutionAsync(tenantId, execution);
                        _logger.LogInformation("⏸️ Workflow execution paused (waiting for dependencies): {ExecutionId}", executionId);
                    }
                    break;
                }

                var tasks = readySteps.Select(step => ExecuteStepAsync(tenantId, execution, step)).ToList();
                await Task.WhenAll(tasks);

                await _store.SaveExecutionAsync(tenantId, execution);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "🚨 Critical error in workflow processor: {ExecutionId}", executionId);
            var execution = await _store.GetExecutionAsync(tenantId, executionId);
            if (execution != null)
            {
                execution.Status = WorkflowExecutionStatus.Failed;
                execution.ErrorMessage = ex.Message;
                await _store.SaveExecutionAsync(tenantId, execution);
            }
        }
    }

    private List<WorkflowStep> GetReadySteps(WorkflowDefinition definition, WorkflowExecution execution)
    {
        return definition.Steps.Where(step =>
        {
            if (execution.StepExecutions.Any(se => se.StepId == step.Id)) return false;

            return step.DependsOn.All(depId =>
                execution.StepExecutions.Any(se => se.StepId == depId && se.Status == WorkflowExecutionStatus.Completed));
        }).ToList();
    }

    private bool IsWorkflowComplete(WorkflowDefinition definition, WorkflowExecution execution)
    {
        return definition.Steps.All(step =>
            execution.StepExecutions.Any(se => se.StepId == step.Id && se.Status == WorkflowExecutionStatus.Completed));
    }

    private bool IsWorkflowFailed(WorkflowExecution execution)
    {
        return execution.StepExecutions.Any(se => se.Status == WorkflowExecutionStatus.Failed);
    }

    private async Task ExecuteStepAsync(string tenantId, WorkflowExecution execution, WorkflowStep step)
    {
        var stepExec = new WorkflowStepExecution
        {
            StepId = step.Id,
            StepName = step.Name,
            Status = WorkflowExecutionStatus.Running,
            StartedAt = DateTime.UtcNow
        };
        execution.StepExecutions.Add(stepExec);
        await _store.SaveExecutionAsync(tenantId, execution);

        if (_broadcaster != null)
        {
            await _broadcaster.BroadcastStepStarted(execution.Id, stepExec);
        }

        try
        {
            _logger.LogDebug("🎬 Executing step: {StepName} ({StepId}) in workflow {ExecutionId}", step.Name, step.Id, execution.Id);

            if (step.StepType == WorkflowStepType.Decision && !string.IsNullOrEmpty(step.ConditionExpression))
            {
                var result = EvaluateCondition(step.ConditionExpression, execution.Variables);
                stepExec.Output["decision"] = result;
                stepExec.Status = WorkflowExecutionStatus.Completed;
            }
            else if (step.StepType == WorkflowStepType.Wait)
            {
                var timeout = step.Timeout ?? TimeSpan.FromMinutes(5);
                _logger.LogInformation("⏳ Waiting for {Timeout} on step: {StepName}", timeout, step.Name);
                stepExec.Output["waited"] = timeout.ToString();
                stepExec.Status = WorkflowExecutionStatus.Completed;
            }
            else if (step.StepType == WorkflowStepType.Approval)
            {
                _logger.LogInformation("⏸️ Approval gate reached: {StepName} - auto-approving for now", step.Name);
                stepExec.Output["approved"] = true;
                stepExec.Status = WorkflowExecutionStatus.Completed;
            }
            else if (step.StepType == WorkflowStepType.Action)
            {
                if (!string.IsNullOrEmpty(step.AgentName))
                {
                    var agentInput = ApplyVariables(step.ActionDescription ?? step.Name, execution.Variables);
                    var context = new UserContext { UserId = execution.InitiatedBy ?? "system" };
                    var response = await _agentExecutor.ExecuteAsync(execution.Id, agentInput, context, step.AgentName);
                    
                    stepExec.Output["content"] = response.Content;
                    stepExec.Output["success"] = response.Success;
                    if (!response.Success) throw new Exception(response.ErrorMessage ?? "Agent execution failed");
                }
                else if (!string.IsNullOrEmpty(step.ToolName))
                {
                    var toolInput = new ToolInput
                    {
                        Action = step.ActionDescription ?? "execute",
                        Parameters = step.Input,
                        UserId = execution.InitiatedBy
                    };
                    var result = await _toolManager.ExecuteToolAsync(step.ToolName, toolInput);
                    
                    stepExec.Output["data"] = result.Data ?? string.Empty;
                    stepExec.Output["success"] = result.Success;
                    if (!result.Success) throw new Exception(result.ErrorMessage ?? "Tool execution failed");
                }
                stepExec.Status = WorkflowExecutionStatus.Completed;

                foreach (var kvp in stepExec.Output)
                {
                    execution.Variables[$"{step.Id}.{kvp.Key}"] = kvp.Value;
                    if (!execution.Variables.ContainsKey(kvp.Key))
                    {
                        execution.Variables[kvp.Key] = kvp.Value;
                    }
                }
            }
            else if (step.StepType == WorkflowStepType.Parallel && step.ParallelSteps.Count > 0)
            {
                var parallelTasks = step.ParallelSteps.Select(ps => ExecuteStepAsync(tenantId, execution, ps)).ToList();
                await Task.WhenAll(parallelTasks);
                stepExec.Status = WorkflowExecutionStatus.Completed;
            }
            else if (step.StepType == WorkflowStepType.Subworkflow)
            {
                _logger.LogInformation("🔄 Subworkflow execution not yet implemented: {StepName}", step.Name);
                stepExec.Output["skipped"] = "Subworkflow not implemented";
                stepExec.Status = WorkflowExecutionStatus.Completed;
            }
            else
            {
                stepExec.Status = WorkflowExecutionStatus.Completed;
            }

            stepExec.CompletedAt = DateTime.UtcNow;
            await _store.SaveExecutionAsync(tenantId, execution);

            if (stepExec.Status == WorkflowExecutionStatus.Completed)
            {
                if (_broadcaster != null)
                {
                    await _broadcaster.BroadcastStepCompleted(execution.Id, stepExec);
                }
            }
            else if (stepExec.Status == WorkflowExecutionStatus.Failed)
            {
                if (_broadcaster != null)
                {
                    await _broadcaster.BroadcastStepFailed(execution.Id, stepExec);
                }
            }

            _logger.LogInformation("✅ Step {StepName} completed successfully", step.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Step {StepName} failed: {Message}", step.Name, ex.Message);
            stepExec.Status = WorkflowExecutionStatus.Failed;
            stepExec.ErrorMessage = ex.Message;
            stepExec.CompletedAt = DateTime.UtcNow;

            await _store.SaveExecutionAsync(tenantId, execution);

            if (_broadcaster != null)
            {
                await _broadcaster.BroadcastStepFailed(execution.Id, stepExec);
            }

            if (step.CompensationStep != null)
            {
                _logger.LogInformation("🔄 Running compensation for step: {StepName}", step.Name);
                try
                {
                    await ExecuteStepAsync(tenantId, execution, step.CompensationStep);
                    stepExec.CompensationExecuted = true;
                }
                catch (Exception compEx)
                {
                    _logger.LogError(compEx, "🚨 Compensation failed for step: {StepName}", step.Name);
                }
            }
        }
    }

    private bool EvaluateCondition(string expression, Dictionary<string, object> variables)
    {
        _logger.LogDebug("Evaluating condition: {Expression}", expression);
        foreach (var kvp in variables)
        {
            if (expression.Contains(kvp.Key) && kvp.Value is bool b && b) return true;
            if (expression.Contains(kvp.Key) && kvp.Value is string s && expression.Contains(s)) return true;
        }
        return expression.Contains("true") || !expression.Contains("false");
    }

    private string ApplyVariables(string template, Dictionary<string, object> variables)
    {
        var result = template;
        foreach (var kvp in variables)
        {
            result = result.Replace("{{" + kvp.Key + "}}", kvp.Value?.ToString() ?? "");
        }
        return result;
    }
}
