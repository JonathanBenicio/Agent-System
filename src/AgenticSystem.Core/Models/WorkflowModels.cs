namespace AgenticSystem.Core.Models;

// ═══════════════════════════════════════════════════════════
// Workflow Engine — Advanced Orchestration
// ═══════════════════════════════════════════════════════════

/// <summary>
/// A workflow definition with support for parallel execution, branching, and compensation.
/// </summary>
public class WorkflowDefinition
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public int Version { get; set; } = 1;
    public List<WorkflowStep> Steps { get; init; } = [];
    public Dictionary<string, object> Variables { get; init; } = new();
    public WorkflowTriggerType TriggerType { get; init; } = WorkflowTriggerType.Manual;
    public string? CronExpression { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// A single step in a workflow with execution semantics.
/// </summary>
public class WorkflowStep
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Name { get; init; } = string.Empty;
    public WorkflowStepType StepType { get; init; } = WorkflowStepType.Action;
    public string? AgentName { get; init; }
    public string? ToolName { get; init; }
    public string? ActionDescription { get; init; }
    public Dictionary<string, object> Input { get; init; } = new();
    public Dictionary<string, object> Output { get; set; } = new();

    // ─── Flow control ───
    public List<string> DependsOn { get; init; } = []; // Step IDs that must complete first
    public string? ConditionExpression { get; init; } // e.g., "{{previousStep.result}} == 'success'"
    public List<WorkflowStep> ParallelSteps { get; init; } = []; // Steps to run in parallel
    public WorkflowStep? CompensationStep { get; init; } // Undo action on failure

    // ─── Error handling ───
    public int MaxRetries { get; init; } = 0;
    public TimeSpan? Timeout { get; init; }
    public WorkflowErrorStrategy ErrorStrategy { get; init; } = WorkflowErrorStrategy.Fail;
}

/// <summary>
/// Runtime instance of a workflow execution.
/// </summary>
public class WorkflowExecution
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string TenantId { get; set; } = "default";
    public string WorkflowId { get; init; } = string.Empty;
    public string WorkflowName { get; init; } = string.Empty;
    public WorkflowExecutionStatus Status { get; set; } = WorkflowExecutionStatus.Pending;
    public List<WorkflowStepExecution> StepExecutions { get; init; } = [];
    public Dictionary<string, object> Variables { get; set; } = new();
    public string? InitiatedBy { get; init; }
    public string? ErrorMessage { get; set; }
    public DateTime StartedAt { get; init; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public TimeSpan? Duration => CompletedAt.HasValue ? CompletedAt.Value - StartedAt : null;
}

/// <summary>
/// Runtime execution state of a single workflow step.
/// </summary>
public class WorkflowStepExecution
{
    public string StepId { get; init; } = string.Empty;
    public string StepName { get; init; } = string.Empty;
    public WorkflowExecutionStatus Status { get; set; } = WorkflowExecutionStatus.Pending;
    public Dictionary<string, object> Output { get; set; } = new();
    public string? ErrorMessage { get; set; }
    public int RetryCount { get; set; }
    public bool CompensationExecuted { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public enum WorkflowStepType
{
    Action,      // Execute a tool or agent
    Decision,    // Branch based on condition
    Parallel,    // Execute sub-steps in parallel
    Wait,        // Wait for external event or timer
    Approval,    // Human approval gate
    Subworkflow  // Execute another workflow
}

public enum WorkflowExecutionStatus
{
    Pending,
    Running,
    Paused,
    WaitingForApproval,
    Completed,
    Failed,
    Cancelled,
    Compensating
}

public enum WorkflowErrorStrategy
{
    Fail,         // Fail the entire workflow
    Skip,         // Skip this step, continue
    Retry,        // Retry with exponential backoff
    Compensate    // Run compensation and continue
}

public enum WorkflowTriggerType
{
    Manual,
    Scheduled,
    Event,
    Webhook
}
