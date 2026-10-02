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
    private readonly AsyncLocal<bool> _deferPersistence = new();
    private readonly IDirectAgentRequestExecutor _agentExecutor;
    private readonly IToolManager _toolManager;
    private readonly IPermissionService? _permissionService;
    private readonly IWorkflowEventBroadcaster? _broadcaster;
    private readonly ILogger<DefaultWorkflowEngine> _logger;

    public DefaultWorkflowEngine(
        IWorkflowStore store,
        IDirectAgentRequestExecutor agentExecutor,
        IToolManager toolManager,
        ILogger<DefaultWorkflowEngine> logger,
        IWorkflowEventBroadcaster? broadcaster = null,
        IPermissionService? permissionService = null)
    {
        _store = store;
        _agentExecutor = agentExecutor;
        _toolManager = toolManager;
        _permissionService = permissionService;
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
        WorkflowGraphValidator.Validate(workflow);

        var definitionSnapshot = JsonSerializer.Serialize(workflow);
        var execution = new WorkflowExecution
        {
            WorkflowId = workflow.Id,
            WorkflowName = workflow.Name,
            TenantId = tenantId,
            Status = WorkflowExecutionStatus.Pending,
            WorkflowDefinitionVersion = workflow.Version,
            WorkflowDefinitionHash = HashDefinitionSnapshot(definitionSnapshot),
            WorkflowDefinitionSnapshotJson = definitionSnapshot,
            Variables = initialVariables ?? new(),
            InitiatedBy = initiatedBy,
            StartedAt = DateTime.UtcNow
        };

        await SaveExecutionAsync(tenantId, execution, ct);

        if (_broadcaster != null)
            await _broadcaster.BroadcastExecutionStarted(execution);

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
        _ = RestoreDefinitionSnapshot(execution);

        if (additionalInput != null)
        {
            foreach (var kvp in additionalInput)
            {
                execution.Variables[kvp.Key] = kvp.Value;
            }
        }

        execution.Status = WorkflowExecutionStatus.Pending;
        await SaveExecutionAsync(tenantId, execution, ct);

        return execution;
    }

    public Task<WorkflowExecution> ApproveAsync(
        string tenantId,
        string executionId,
        string approvedBy,
        CancellationToken ct = default)
        => ApproveStepAsync(tenantId, executionId, null!, approvedBy, ct);

    public async Task<WorkflowExecution> ApproveStepAsync(
        string tenantId, string executionId, string stepId, string approvedBy, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(approvedBy);
        var execution = await GetPendingApprovalExecutionAsync(tenantId, executionId, ct);
        var definition = RestoreDefinitionSnapshot(execution);
        var step = GetPendingApprovalStep(execution, stepId);

        step.Status = WorkflowExecutionStatus.Completed;
        step.Output["approved"] = true;
        step.Output["approvedBy"] = approvedBy;
        step.CompletedAt = DateTime.UtcNow;
        execution.Status = execution.StepExecutions.Any(item => item.Status == WorkflowExecutionStatus.WaitingForApproval)
            ? WorkflowExecutionStatus.WaitingForApproval : WorkflowExecutionStatus.Pending;
        await SaveExecutionAsync(tenantId, execution, ct);

        if (_broadcaster is not null)
            await _broadcaster.BroadcastStepCompleted(tenantId, executionId, step);

        return execution;
    }

    public Task<WorkflowExecution> RejectAsync(
        string tenantId,
        string executionId,
        string rejectedBy,
        string? reason = null,
        CancellationToken ct = default)
        => RejectStepAsync(tenantId, executionId, null!, rejectedBy, reason, ct);

    public async Task<WorkflowExecution> RejectStepAsync(
        string tenantId, string executionId, string stepId, string rejectedBy,
        string? reason = null, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rejectedBy);
        var execution = await GetPendingApprovalExecutionAsync(tenantId, executionId, ct);
        var step = GetPendingApprovalStep(execution, stepId);
        var rejectionReason = string.IsNullOrWhiteSpace(reason) ? "Rejected by approver." : reason;

        step.Status = WorkflowExecutionStatus.Failed;
        step.ErrorMessage = rejectionReason;
        step.Output["approved"] = false;
        step.Output["rejectedBy"] = rejectedBy;
        step.CompletedAt = DateTime.UtcNow;
        execution.Status = WorkflowExecutionStatus.Cancelled;
        execution.ErrorMessage = rejectionReason;
        execution.CompletedAt = DateTime.UtcNow;
        await SaveExecutionAsync(tenantId, execution, ct);

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

        await SaveExecutionAsync(tenantId, execution, ct);

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

    public async Task ProcessClaimedExecutionAsync(WorkflowExecutionClaim claim, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(claim);
        var execution = await _store.GetExecutionAsync(claim.TenantId, claim.ExecutionId, ct)
            ?? throw new ArgumentException("Execution not found", nameof(claim));
        if (!string.Equals(execution.LeaseOwner, claim.WorkerId, StringComparison.Ordinal)
            || execution.Status != WorkflowExecutionStatus.Running)
        {
            throw new WorkflowExecutionLeaseLostException(claim.ExecutionId);
        }

        var definition = RestoreDefinitionSnapshot(execution);
        await ProcessExecutionAsync(claim.TenantId, claim.ExecutionId, claim.WorkerId, definition, ct);
    }

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

    private static WorkflowStepExecution GetPendingApprovalStep(WorkflowExecution execution, string? stepId)
    {
        var pending = execution.StepExecutions
            .Where(step => step.Status == WorkflowExecutionStatus.WaitingForApproval).ToArray();
        if (!string.IsNullOrWhiteSpace(stepId))
            return pending.SingleOrDefault(step => step.StepId == stepId)
                ?? throw new ArgumentException("Approval step is not pending.", nameof(stepId));
        if (pending.Length != 1)
            throw new WorkflowApprovalAmbiguousException(pending.Select(step => step.StepId).ToArray());
        return pending[0];
    }

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

    private async Task ProcessExecutionAsync(
        string tenantId,
        string executionId,
        string workerId,
        WorkflowDefinition definition,
        CancellationToken ct)
    {
        try
        {
            while (true)
            {
                var execution = await _store.GetExecutionAsync(tenantId, executionId, ct);
                if (execution == null || execution.Status != WorkflowExecutionStatus.Running) break;
                if (!string.Equals(execution.LeaseOwner, workerId, StringComparison.Ordinal)
                    || execution.LeaseExpiresAt <= DateTime.UtcNow)
                    throw new WorkflowExecutionLeaseLostException(executionId);

                var readySteps = GetReadySteps(definition, execution);
                if (readySteps.Count == 0)
                {
                    if (IsWorkflowComplete(definition, execution))
                    {
                        execution.Status = WorkflowExecutionStatus.Completed;
                        execution.CompletedAt = DateTime.UtcNow;
                        await SaveExecutionAsync(tenantId, execution);
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
                        await SaveExecutionAsync(tenantId, execution);
                        if (_broadcaster != null)
                        {
                            await _broadcaster.BroadcastExecutionFailed(execution);
                        }
                        _logger.LogWarning("❌ Workflow execution failed: {ExecutionId}", executionId);
                    }
                    else
                    {
                        var nextWait = execution.StepExecutions
                            .Where(step => step.Status == WorkflowExecutionStatus.Pending && step.WaitUntilUtc.HasValue)
                            .Select(step => step.WaitUntilUtc!.Value)
                            .Where(waitUntil => waitUntil > DateTime.UtcNow)
                            .OrderBy(waitUntil => waitUntil)
                            .FirstOrDefault();
                        execution.Status = nextWait == default
                            ? WorkflowExecutionStatus.Paused
                            : WorkflowExecutionStatus.Pending;
                        await SaveExecutionAsync(tenantId, execution);
                        _logger.LogInformation(
                            nextWait == default
                                ? "Workflow execution paused (waiting for dependencies): {ExecutionId}"
                                : "Workflow execution scheduled to resume at {WaitUntilUtc}: {ExecutionId}",
                            nextWait == default ? executionId : nextWait,
                            executionId);
                    }
                    break;
                }

                await ExecuteParallelStepsAsync(tenantId, execution, readySteps, ct);

                await SaveExecutionAsync(tenantId, execution, ct);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            _logger.LogInformation("Workflow execution {ExecutionId} stopped because its worker lease or host was cancelled.", executionId);
        }
        catch (WorkflowExecutionLeaseLostException ex)
        {
            _logger.LogWarning(ex, "Workflow execution {ExecutionId} was observed by a worker that no longer owns its lease.", executionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "🚨 Critical error in workflow processor: {ExecutionId}", executionId);
            var execution = await _store.GetExecutionAsync(tenantId, executionId, ct);
            if (execution is { Status: WorkflowExecutionStatus.Running }
                && string.Equals(execution.LeaseOwner, workerId, StringComparison.Ordinal)
                && execution.LeaseExpiresAt > DateTime.UtcNow)
            {
                execution.Status = WorkflowExecutionStatus.Failed;
                execution.ErrorMessage = ex.Message;
                await SaveExecutionAsync(tenantId, execution, ct);
            }
        }
    }


    private Task SaveExecutionAsync(string tenantId, WorkflowExecution execution, CancellationToken ct = default)
        => _deferPersistence.Value ? Task.CompletedTask : _store.SaveExecutionAsync(tenantId, execution, ct);

    private async Task ExecuteParallelStepsAsync(
        string tenantId, WorkflowExecution execution, IReadOnlyList<WorkflowStep> steps, CancellationToken ct)
    {
        if (steps.Count == 1)
        {
            await ExecuteStepAsync(tenantId, execution, steps[0], ct);
            return;
        }
        var branches = steps.Select(step => (Step: step, State: CloneForBranch(execution))).ToArray();
        await Task.WhenAll(branches.Select(async branch =>
        {
            var previous = _deferPersistence.Value;
            _deferPersistence.Value = true;
            try { await ExecuteStepAsync(tenantId, branch.State, branch.Step, ct); }
            finally { _deferPersistence.Value = previous; }
        }));
        // Definition order decides the first unqualified output; namespaced outputs
        // remain distinct. No branch observes another branch's intermediate results.
        foreach (var branch in branches)
        {
            var ids = BranchStepIds(branch.Step).ToHashSet(StringComparer.Ordinal);
            foreach (var item in branch.State.StepExecutions.Where(item => ids.Contains(item.StepId)))
            {
                execution.StepExecutions.RemoveAll(existing => existing.StepId == item.StepId);
                execution.StepExecutions.Add(item);
            }
            foreach (var pair in branch.State.Variables)
                if (!execution.Variables.ContainsKey(pair.Key) || ids.Any(id => pair.Key.StartsWith(id + ".", StringComparison.Ordinal)))
                    execution.Variables[pair.Key] = pair.Value;
        }
        if (execution.StepExecutions.Any(step => step.Status == WorkflowExecutionStatus.WaitingForApproval))
            execution.Status = WorkflowExecutionStatus.WaitingForApproval;
        else if (execution.StepExecutions.Any(step => step.Status == WorkflowExecutionStatus.Pending && step.WaitUntilUtc.HasValue))
            execution.Status = WorkflowExecutionStatus.Pending;
    }

    private static IEnumerable<string> BranchStepIds(WorkflowStep step)
    {
        yield return step.Id;
        foreach (var child in step.ParallelSteps)
            foreach (var id in BranchStepIds(child)) yield return id;
        if (step.CompensationStep is { } compensation)
            foreach (var id in BranchStepIds(compensation)) yield return id;
    }

    private static WorkflowExecution CloneForBranch(WorkflowExecution execution) => new()
    {
        Id = execution.Id, TenantId = execution.TenantId, WorkflowId = execution.WorkflowId,
        WorkflowName = execution.WorkflowName, WorkflowDefinitionVersion = execution.WorkflowDefinitionVersion,
        WorkflowDefinitionHash = execution.WorkflowDefinitionHash,
        WorkflowDefinitionSnapshotJson = execution.WorkflowDefinitionSnapshotJson,
        LeaseOwner = execution.LeaseOwner, LeaseExpiresAt = execution.LeaseExpiresAt,
        Status = execution.Status, InitiatedBy = execution.InitiatedBy, ErrorMessage = execution.ErrorMessage,
        StartedAt = execution.StartedAt, CompletedAt = execution.CompletedAt,
        Variables = new Dictionary<string, object>(execution.Variables),
        StepExecutions = execution.StepExecutions.Select(step => new WorkflowStepExecution
        {
            StepId = step.StepId, StepName = step.StepName, Status = step.Status,
            Output = new Dictionary<string, object>(step.Output), ErrorMessage = step.ErrorMessage,
            RetryCount = step.RetryCount, CompensationExecuted = step.CompensationExecuted,
            StartedAt = step.StartedAt, CompletedAt = step.CompletedAt, WaitUntilUtc = step.WaitUntilUtc
        }).ToList()
    };

    private List<WorkflowStep> GetReadySteps(WorkflowDefinition definition, WorkflowExecution execution)
    {
        return definition.Steps.Where(step =>
        {
            var priorExecution = execution.StepExecutions.FirstOrDefault(se => se.StepId == step.Id);
            if (priorExecution is not null && priorExecution.Status != WorkflowExecutionStatus.Pending) return false;
            if (priorExecution?.WaitUntilUtc is { } waitUntil && waitUntil > DateTime.UtcNow) return false;

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

    private async Task ExecuteStepAsync(string tenantId, WorkflowExecution execution, WorkflowStep step, CancellationToken executionToken)
    {
        var stepExec = execution.StepExecutions.FirstOrDefault(existing =>
            existing.StepId == step.Id && existing.Status == WorkflowExecutionStatus.Pending);
        if (stepExec is null)
        {
            stepExec = new WorkflowStepExecution { StepId = step.Id, StepName = step.Name };
            execution.StepExecutions.Add(stepExec);
        }
        else
        {
            if (!stepExec.WaitUntilUtc.HasValue)
                stepExec.RetryCount++;
            stepExec.Output.Clear();
            stepExec.ErrorMessage = null;
        }
        stepExec.Status = WorkflowExecutionStatus.Running;
        stepExec.StartedAt = DateTime.UtcNow;
        stepExec.CompletedAt = null;
        await SaveExecutionAsync(tenantId, execution, executionToken);

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
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(stepCts.Token, executionToken);
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
                if (!stepExec.WaitUntilUtc.HasValue)
                {
                    var timeout = step.Timeout ?? TimeSpan.FromMinutes(5);
                    stepExec.WaitUntilUtc = DateTime.UtcNow.Add(timeout);
                    stepExec.Status = WorkflowExecutionStatus.Pending;
                    execution.Status = WorkflowExecutionStatus.Pending;
                    await SaveExecutionAsync(tenantId, execution, ct);
                    return;
                }

                if (stepExec.WaitUntilUtc.Value > DateTime.UtcNow)
                {
                    stepExec.Status = WorkflowExecutionStatus.Pending;
                    execution.Status = WorkflowExecutionStatus.Pending;
                    await SaveExecutionAsync(tenantId, execution, ct);
                    return;
                }

                stepExec.Output["waited"] = step.Timeout?.ToString() ?? TimeSpan.FromMinutes(5).ToString();
                stepExec.WaitUntilUtc = null;
                stepExec.Status = WorkflowExecutionStatus.Completed;
            }
            else if (step.StepType == WorkflowStepType.Approval)
            {
                _logger.LogInformation("⏸️ Approval gate reached: {StepName}", step.Name);
                stepExec.Status = WorkflowExecutionStatus.WaitingForApproval;
                execution.Status = WorkflowExecutionStatus.WaitingForApproval;
                await SaveExecutionAsync(tenantId, execution);
                if (_broadcaster is not null)
                    await _broadcaster.BroadcastApprovalRequested(execution, stepExec);
                return;
            }
            else if (step.StepType is WorkflowStepType.Action or WorkflowStepType.Agent)
            {
                if (!string.IsNullOrEmpty(step.AgentName))
                {
                    if (string.IsNullOrWhiteSpace(execution.InitiatedBy))
                        throw new UnauthorizedAccessException("An authenticated initiator is required for workflow agent steps.");
                    var agentInput = ApplyVariables(
                        step.ActionDescription ?? DefinitionPrompt(execution) ?? step.Name,
                        execution.Variables);
                    var dependencyOutputs = step.DependsOn.Select(id => execution.StepExecutions
                        .FirstOrDefault(item => item.StepId == id)?.Output.GetValueOrDefault("content"))
                        .Where(output => output is not null).Select(output => output!.ToString());
                    agentInput = string.Join("\n", new[] { agentInput }.Concat(dependencyOutputs));
                    var context = new UserContext
                    {
                        UserId = execution.InitiatedBy,
                        TenantId = tenantId,
                        WorkflowOptions = new WorkflowAgentOptions($"workflow:{execution.Id}:{step.Id}", step.ModelOverride, step.AllowedToolsOverride,
                            execution.Variables.GetValueOrDefault("imagePath")?.ToString())
                    };
                    context.Preferences["workflowIdempotencyKey"] = $"{execution.Id}:{step.Id}";
                    var response = await _agentExecutor.ExecuteAsync(execution.Id, agentInput, context, step.AgentName, ct);

                    stepExec.Output["content"] = response.Content;
                    stepExec.Output["success"] = response.Success;
                    if (!response.Success) throw new Exception(response.ErrorMessage ?? "Agent execution failed");
                }
                else if (!string.IsNullOrEmpty(step.ToolName))
                {
                    if (string.IsNullOrWhiteSpace(execution.InitiatedBy)
                        || _permissionService is null
                        || !await _permissionService.HasPermissionAsync(
                            execution.InitiatedBy,
                            $"tools/{step.ToolName}",
                            Permission.Execute,
                            ct))
                    {
                        throw new UnauthorizedAccessException(
                            $"The workflow initiator is not authorized to execute tool '{step.ToolName}'.");
                    }

                    var toolInput = new ToolInput
                    {
                        Action = step.ActionDescription ?? "execute",
                        Parameters = step.Input,
                        UserId = execution.InitiatedBy,
                        IdempotencyKey = $"{execution.Id}:{step.Id}"
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
                await ExecuteParallelStepsAsync(tenantId, execution, step.ParallelSteps, ct);
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
            await SaveExecutionAsync(tenantId, execution);

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
        catch (OperationCanceledException) when (!executionToken.IsCancellationRequested)
        {
            var timeoutDuration = step.Timeout ?? TimeSpan.FromMinutes(5);
            _logger.LogWarning("⏰ Step {StepName} timed out after {Timeout}", step.Name, timeoutDuration);
            await HandleStepFailureAsync(
                tenantId,
                execution,
                step,
                stepExec,
                $"Step timed out after {timeoutDuration.TotalMinutes:F0} minutes.",
                executionToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Step {StepName} failed: {Message}", step.Name, ex.Message);
            await HandleStepFailureAsync(tenantId, execution, step, stepExec, ex.Message, executionToken);
        }
    }

    private static string? DefinitionPrompt(WorkflowExecution execution) =>
        RestoreDefinitionSnapshot(execution).PromptTemplate;

    private async Task HandleStepFailureAsync(
        string tenantId,
        WorkflowExecution execution,
        WorkflowStep step,
        WorkflowStepExecution stepExecution,
        string errorMessage,
        CancellationToken ct)
    {
        var retryScheduled = stepExecution.RetryCount < step.MaxRetries;
        stepExecution.Status = retryScheduled ? WorkflowExecutionStatus.Pending : WorkflowExecutionStatus.Failed;
        stepExecution.ErrorMessage = errorMessage;
        stepExecution.CompletedAt = retryScheduled ? null : DateTime.UtcNow;
        await SaveExecutionAsync(tenantId, execution, ct);

        if (retryScheduled)
            return;

        if (_broadcaster is not null)
            await _broadcaster.BroadcastStepFailed(tenantId, execution.Id, stepExecution);

        if (step.CompensationStep is null)
            return;

        try
        {
            await ExecuteStepAsync(tenantId, execution, step.CompensationStep, ct);
            stepExecution.CompensationExecuted = true;
            await SaveExecutionAsync(tenantId, execution, ct);
        }
        catch (Exception compensationException)
        {
            _logger.LogError(compensationException, "Compensation failed for step: {StepName}", step.Name);
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
