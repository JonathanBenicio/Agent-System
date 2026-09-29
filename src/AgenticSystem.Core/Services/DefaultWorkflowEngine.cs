using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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
        string tenantId,
        WorkflowDefinition workflow,
        Dictionary<string, object>? initialVariables = null,
        string? initiatedBy = null,
        CancellationToken ct = default)
    {
        _logger.LogInformation("🚀 Starting workflow: {WorkflowName} ({WorkflowId}) for tenant {TenantId}", workflow.Name, workflow.Id, tenantId);

        var definitionSnapshot = JsonSerializer.Serialize(workflow);
        var execution = new WorkflowExecution
        {
            WorkflowId = workflow.Id,
            WorkflowName = workflow.Name,
            TenantId = tenantId,
            Status = WorkflowExecutionStatus.Running,
            WorkflowDefinitionVersion = workflow.Version,
            WorkflowDefinitionHash = HashDefinitionSnapshot(definitionSnapshot),
            WorkflowDefinitionSnapshotJson = definitionSnapshot,
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
        string tenantId,
        string executionId,
        Dictionary<string, object>? additionalInput = null,
        CancellationToken ct = default)
    {
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

        var definition = RestoreDefinitionSnapshot(execution);

        _ = Task.Run(() => ProcessExecutionAsync(tenantId, execution.Id, definition));

        return execution;
    }

    public async Task<WorkflowExecution> ApproveAsync(
        string tenantId,
        string executionId,
        string approvedBy,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(approvedBy);
        var execution = await GetPendingApprovalExecutionAsync(tenantId, executionId, ct);
        var definition = RestoreDefinitionSnapshot(execution);
        var step = GetPendingApprovalStep(execution);

        step.Status = WorkflowExecutionStatus.Completed;
        step.Output["approved"] = true;
        step.Output["approvedBy"] = approvedBy;
        step.CompletedAt = DateTime.UtcNow;
        execution.Status = WorkflowExecutionStatus.Running;
        await _store.SaveExecutionAsync(tenantId, execution, ct);

        if (_broadcaster is not null)
            await _broadcaster.BroadcastStepCompleted(tenantId, executionId, step);

        _ = Task.Run(() => ProcessExecutionAsync(tenantId, execution.Id, definition), ct);
        return execution;
    }

    public async Task<WorkflowExecution> RejectAsync(
        string tenantId,
        string executionId,
        string rejectedBy,
        string? reason = null,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rejectedBy);
        var execution = await GetPendingApprovalExecutionAsync(tenantId, executionId, ct);
        var step = GetPendingApprovalStep(execution);
        var rejectionReason = string.IsNullOrWhiteSpace(reason) ? "Rejected by approver." : reason;

        step.Status = WorkflowExecutionStatus.Failed;
        step.ErrorMessage = rejectionReason;
        step.Output["approved"] = false;
        step.Output["rejectedBy"] = rejectedBy;
        step.CompletedAt = DateTime.UtcNow;
        execution.Status = WorkflowExecutionStatus.Cancelled;
        execution.ErrorMessage = rejectionReason;
        execution.CompletedAt = DateTime.UtcNow;
        await _store.SaveExecutionAsync(tenantId, execution, ct);

        if (_broadcaster is not null)
        {
            await _broadcaster.BroadcastStepFailed(tenantId, executionId, step);
            await _broadcaster.BroadcastExecutionCancelled(execution);
        }

        return execution;
    }

    public async Task<WorkflowExecution> CancelAsync(
        string tenantId,
        string executionId,
        string? reason = null,
        CancellationToken ct = default)
    {
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

    public Task<WorkflowExecution?> GetExecutionAsync(string tenantId, string executionId, CancellationToken ct = default)
        => _store.GetExecutionAsync(tenantId, executionId, ct);

    public Task<IReadOnlyList<WorkflowExecution>> ListExecutionsAsync(string tenantId, WorkflowExecutionStatus? statusFilter = null, int limit = 20, CancellationToken ct = default)
        => _store.ListExecutionsAsync(tenantId, statusFilter, limit, ct);

    private async Task<WorkflowExecution> GetPendingApprovalExecutionAsync(
        string tenantId,
        string executionId,
        CancellationToken ct)
    {
        var execution = await _store.GetExecutionAsync(tenantId, executionId, ct)
            ?? throw new ArgumentException("Execution not found", nameof(executionId));
        if (execution.Status != WorkflowExecutionStatus.WaitingForApproval
            || !execution.StepExecutions.Any(step => step.Status == WorkflowExecutionStatus.WaitingForApproval))
        {
            throw new InvalidOperationException("Workflow execution has no pending approval step.");
        }

        return execution;
    }

    private static WorkflowStepExecution GetPendingApprovalStep(WorkflowExecution execution) =>
        execution.StepExecutions.Single(step => step.Status == WorkflowExecutionStatus.WaitingForApproval);

    private static WorkflowDefinition RestoreDefinitionSnapshot(WorkflowExecution execution)
    {
        if (string.IsNullOrWhiteSpace(execution.WorkflowDefinitionSnapshotJson)
            || string.IsNullOrWhiteSpace(execution.WorkflowDefinitionHash))
        {
            throw new InvalidOperationException(
                "This workflow execution has no immutable definition snapshot and cannot be resumed safely.");
        }

        var actualHash = HashDefinitionSnapshot(execution.WorkflowDefinitionSnapshotJson);
        if (!string.Equals(actualHash, execution.WorkflowDefinitionHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The workflow definition snapshot hash does not match the stored execution.");

        var definition = JsonSerializer.Deserialize<WorkflowDefinition>(execution.WorkflowDefinitionSnapshotJson)
            ?? throw new InvalidOperationException("The workflow definition snapshot is invalid.");
        if (!string.Equals(definition.Id, execution.WorkflowId, StringComparison.Ordinal)
            || definition.Version != execution.WorkflowDefinitionVersion)
        {
            throw new InvalidOperationException("The workflow definition snapshot identity does not match the stored execution.");
        }

        return definition;
    }

    private static string HashDefinitionSnapshot(string snapshotJson) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(CanonicalizeJson(snapshotJson))));

    private static string CanonicalizeJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
            WriteCanonicalElement(document.RootElement, writer);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteCanonicalElement(JsonElement element, Utf8JsonWriter writer)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject().OrderBy(property => property.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonicalElement(property.Value, writer);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                    WriteCanonicalElement(item, writer);
                writer.WriteEndArray();
                break;
            case JsonValueKind.String:
                writer.WriteStringValue(element.GetString());
                break;
            case JsonValueKind.Number:
                writer.WriteRawValue(element.GetRawText(), skipInputValidation: true);
                break;
            case JsonValueKind.True:
                writer.WriteBooleanValue(true);
                break;
            case JsonValueKind.False:
                writer.WriteBooleanValue(false);
                break;
            case JsonValueKind.Null:
                writer.WriteNullValue();
                break;
            default:
                throw new InvalidOperationException($"Unsupported JSON value kind '{element.ValueKind}' in workflow definition snapshot.");
        }
    }

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
            await _broadcaster.BroadcastStepStarted(tenantId, execution.Id, stepExec);
        }

        try
        {
            // Per-step timeout: Action steps have a configurable timeout (default 5 min).
            // This prevents a single hung LLM call from blocking the entire workflow forever.
            // For Wait steps, Timeout is the requested delay itself, not a watchdog timeout.
            // Applying the same duration as a CancellationTokenSource races the delay and can
            // report a valid wait as a failed step at its deadline.
            var stepTimeout = step.StepType == WorkflowStepType.Wait
                ? System.Threading.Timeout.InfiniteTimeSpan
                : step.Timeout ?? (step.StepType is WorkflowStepType.Action or WorkflowStepType.Agent
                    ? TimeSpan.FromMinutes(5)
                    : TimeSpan.FromMinutes(30));

            using var stepCts = new CancellationTokenSource(stepTimeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(stepCts.Token);
            var ct = linkedCts.Token;

            _logger.LogDebug("🎬 Executing step: {StepName} ({StepId}) in workflow {ExecutionId} (timeout: {Timeout})",
                step.Name, step.Id, execution.Id, stepTimeout);

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
                await Task.Delay(timeout, ct);
                stepExec.Output["waited"] = timeout.ToString();
                stepExec.Status = WorkflowExecutionStatus.Completed;
            }
            else if (step.StepType == WorkflowStepType.Approval)
            {
                _logger.LogInformation("⏸️ Approval gate reached: {StepName}", step.Name);
                stepExec.Status = WorkflowExecutionStatus.WaitingForApproval;
                execution.Status = WorkflowExecutionStatus.WaitingForApproval;
                await _store.SaveExecutionAsync(tenantId, execution);
                if (_broadcaster is not null)
                    await _broadcaster.BroadcastApprovalRequested(execution, stepExec);
                return;
            }
            else if (step.StepType is WorkflowStepType.Action or WorkflowStepType.Agent)
            {
                if (!string.IsNullOrEmpty(step.AgentName))
                {
                    var agentInput = ApplyVariables(step.ActionDescription ?? step.Name, execution.Variables);
                    var context = new UserContext
                    {
                        UserId = execution.InitiatedBy ?? "system",
                        TenantId = tenantId
                    };
                    var response = await _agentExecutor.ExecuteAsync(execution.Id, agentInput, context, step.AgentName, ct);

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
                    var result = await _toolManager.ExecuteToolAsync(step.ToolName, toolInput, ct);
                    
                    stepExec.Output["data"] = result.Data ?? string.Empty;
                    stepExec.Output["success"] = result.Success;
                    if (!result.Success) throw new Exception(result.ErrorMessage ?? "Tool execution failed");
                }
                else
                {
                    throw new InvalidOperationException($"Step '{step.Name}' must configure an agent or tool.");
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
                throw new NotSupportedException($"Subworkflow step '{step.Name}' is not supported by this workflow engine.");
            }
            else
            {
                throw new NotSupportedException($"Workflow step type '{step.StepType}' is not supported.");
            }

            stepExec.CompletedAt = DateTime.UtcNow;
            await _store.SaveExecutionAsync(tenantId, execution);

            if (stepExec.Status == WorkflowExecutionStatus.Completed)
            {
                if (_broadcaster != null)
                {
                await _broadcaster.BroadcastStepCompleted(tenantId, execution.Id, stepExec);
                }
            }
            else if (stepExec.Status == WorkflowExecutionStatus.Failed)
            {
                if (_broadcaster != null)
                {
                    await _broadcaster.BroadcastStepFailed(tenantId, execution.Id, stepExec);
                }
            }

            _logger.LogInformation("✅ Step {StepName} completed successfully", step.Name);
        }
        catch (OperationCanceledException)
        {
            var timeoutDuration = step.Timeout ?? TimeSpan.FromMinutes(5);
            _logger.LogWarning("⏰ Step {StepName} timed out after {Timeout}", step.Name, timeoutDuration);
            stepExec.Status = WorkflowExecutionStatus.Failed;
            stepExec.ErrorMessage = $"Step timed out after {timeoutDuration.TotalMinutes:F0} minutes.";
            stepExec.CompletedAt = DateTime.UtcNow;
            await _store.SaveExecutionAsync(tenantId, execution);
            if (_broadcaster != null) await _broadcaster.BroadcastStepFailed(tenantId, execution.Id, stepExec);
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
                await _broadcaster.BroadcastStepFailed(tenantId, execution.Id, stepExec);
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
