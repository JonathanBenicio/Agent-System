namespace AgenticSystem.Core.Models;

// ═══════════════════════════════════════════════════════════
// Agent Runtime State Machine — Formal execution states
// ═══════════════════════════════════════════════════════════

/// <summary>
/// Formal execution states for an agent runtime.
/// Provides a deterministic state machine for debugging, observability, and retry.
/// </summary>
public enum AgentExecutionState
{
    Created,
    Idle,
    Planning,
    RetrievingContext,
    SelectingTool,
    ExecutingTool,
    CallingLLM,
    Reflecting,
    WaitingHumanApproval,
    Completed,
    Failed,
    Cancelled
}

/// <summary>
/// Records a single state transition in the execution history.
/// </summary>
public record AgentStateTransition
{
    public AgentExecutionState From { get; init; }
    public AgentExecutionState To { get; init; }
    public string Trigger { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public string? AgentName { get; init; }
    public string? Detail { get; init; }
}

/// <summary>
/// Strict state machine for agent execution.
/// Invalid transitions throw <see cref="InvalidOperationException"/>.
/// </summary>
public class AgentExecutionStateMachine
{
    private static readonly Dictionary<AgentExecutionState, HashSet<AgentExecutionState>> ValidTransitions = new()
    {
        [AgentExecutionState.Created] = [AgentExecutionState.Idle, AgentExecutionState.Cancelled],
        [AgentExecutionState.Idle] = [AgentExecutionState.Planning, AgentExecutionState.CallingLLM, AgentExecutionState.Cancelled],
        [AgentExecutionState.Planning] = [AgentExecutionState.RetrievingContext, AgentExecutionState.SelectingTool, AgentExecutionState.CallingLLM, AgentExecutionState.Failed, AgentExecutionState.Cancelled],
        [AgentExecutionState.RetrievingContext] = [AgentExecutionState.SelectingTool, AgentExecutionState.CallingLLM, AgentExecutionState.Planning, AgentExecutionState.Failed, AgentExecutionState.Cancelled],
        [AgentExecutionState.SelectingTool] = [AgentExecutionState.ExecutingTool, AgentExecutionState.WaitingHumanApproval, AgentExecutionState.Failed, AgentExecutionState.Cancelled],
        [AgentExecutionState.ExecutingTool] = [AgentExecutionState.CallingLLM, AgentExecutionState.Reflecting, AgentExecutionState.SelectingTool, AgentExecutionState.Failed, AgentExecutionState.Cancelled],
        [AgentExecutionState.CallingLLM] = [AgentExecutionState.Reflecting, AgentExecutionState.SelectingTool, AgentExecutionState.Completed, AgentExecutionState.Failed, AgentExecutionState.Cancelled],
        [AgentExecutionState.Reflecting] = [AgentExecutionState.Planning, AgentExecutionState.CallingLLM, AgentExecutionState.Completed, AgentExecutionState.Failed, AgentExecutionState.Cancelled],
        [AgentExecutionState.WaitingHumanApproval] = [AgentExecutionState.ExecutingTool, AgentExecutionState.Cancelled, AgentExecutionState.Failed],
        [AgentExecutionState.Completed] = [],
        [AgentExecutionState.Failed] = [AgentExecutionState.Idle],
        [AgentExecutionState.Cancelled] = []
    };

    private readonly List<AgentStateTransition> _history = new();
    private readonly object _lock = new();

    public AgentExecutionState CurrentState { get; private set; } = AgentExecutionState.Created;

    public string? SessionId { get; init; }

    public IReadOnlyList<AgentStateTransition> History
    {
        get { lock (_lock) { return _history.ToList(); } }
    }

    public bool IsTerminal => CurrentState is AgentExecutionState.Completed
                           or AgentExecutionState.Failed
                           or AgentExecutionState.Cancelled;

    /// <summary>
    /// Attempts a state transition. Throws if the transition is invalid.
    /// </summary>
    public AgentStateTransition Transition(AgentExecutionState newState, string trigger, string? agentName = null, string? detail = null)
    {
        lock (_lock)
        {
            if (!CanTransition(newState))
            {
                throw new InvalidOperationException(
                    $"Invalid state transition: {CurrentState} → {newState}. " +
                    $"Valid targets from {CurrentState}: [{string.Join(", ", GetValidTransitions())}].");
            }

            var transition = new AgentStateTransition
            {
                From = CurrentState,
                To = newState,
                Trigger = trigger,
                AgentName = agentName,
                Detail = detail
            };

            _history.Add(transition);
            CurrentState = newState;
            return transition;
        }
    }

    /// <summary>
    /// Checks if a transition to the given state is valid from the current state.
    /// </summary>
    public bool CanTransition(AgentExecutionState target)
    {
        lock (_lock)
        {
            return ValidTransitions.TryGetValue(CurrentState, out var targets) && targets.Contains(target);
        }
    }

    /// <summary>
    /// Returns all valid transition targets from the current state.
    /// </summary>
    public IReadOnlyList<AgentExecutionState> GetValidTransitions()
    {
        lock (_lock)
        {
            return ValidTransitions.TryGetValue(CurrentState, out var targets)
                ? targets.ToList()
                : [];
        }
    }

    /// <summary>
    /// Total elapsed time from first transition to last (or now if still running).
    /// </summary>
    public TimeSpan ElapsedTime
    {
        get
        {
            lock (_lock)
            {
                if (_history.Count == 0) return TimeSpan.Zero;
                var start = _history[0].Timestamp;
                var end = IsTerminal ? _history[^1].Timestamp : DateTime.UtcNow;
                return end - start;
            }
        }
    }
}
