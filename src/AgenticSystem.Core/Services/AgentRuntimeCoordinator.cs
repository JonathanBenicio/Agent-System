using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Channels;
using System.Diagnostics.Metrics;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Core.Services;

public class AgentRuntimeCoordinator : IAgentRuntimeCoordinator
{
    private static readonly HashSet<AgentStreamEventType> PersistedEventTypes =
    [
        AgentStreamEventType.SessionStarted,
        AgentStreamEventType.PlanningStarted,
        AgentStreamEventType.PlanningCompleted,
        AgentStreamEventType.AgentSelected,
        AgentStreamEventType.StepStarted,
        AgentStreamEventType.StepCompleted,
        AgentStreamEventType.StepFailed,
        AgentStreamEventType.ToolStarted,
        AgentStreamEventType.ToolCompleted,
        AgentStreamEventType.ToolApprovalRequired,
        AgentStreamEventType.ToolApprovalResolved,
        AgentStreamEventType.HandoffStarted,
        AgentStreamEventType.HandoffCompleted,
        AgentStreamEventType.RagStarted,
        AgentStreamEventType.RagCompleted,
        AgentStreamEventType.ReviewStarted,
        AgentStreamEventType.ReviewCompleted,
        AgentStreamEventType.FinalApprovalRequired,
        AgentStreamEventType.FinalApprovalResolved,
        AgentStreamEventType.ArtifactRecorded,
        AgentStreamEventType.StateTransition,
        AgentStreamEventType.SessionCompleted,
        AgentStreamEventType.Error
    ];

    private readonly ISessionManager _sessionManager;
    private readonly IEventBus? _eventBus;
    private readonly IOperationalStore? _operationalStore;
    private readonly ILogger<AgentRuntimeCoordinator> _logger;
    private readonly ConcurrentDictionary<string, List<AgentExecutionArtifact>> _artifacts = new();
    private readonly ConcurrentDictionary<string, AgentRuntimeMetricsSnapshot> _sessionMetrics = new();
    private readonly AgentRuntimeMetricsSnapshot _globalMetrics = new();
    private readonly AsyncLocal<RuntimeScope?> _currentScope = new();

    // OpenTelemetry Custom Metrics
    private static readonly Meter _meter = new("AgenticSystem.Runtime", "1.0.0");
    private static readonly Counter<long> _streamStartedCounter = _meter.CreateCounter<long>("agenticsystem.stream.started", description: "Number of streams started");
    private static readonly Counter<long> _agentExecutionsCounter = _meter.CreateCounter<long>("agenticsystem.agent.executions", description: "Number of agent executions");
    private static readonly Counter<long> _toolExecutionsCounter = _meter.CreateCounter<long>("agenticsystem.tool.executions", description: "Number of tool executions");
    private static readonly Counter<long> _errorCounter = _meter.CreateCounter<long>("agenticsystem.errors", description: "Number of runtime errors");

    public AgentRuntimeCoordinator(
        ISessionManager sessionManager, 
        ILogger<AgentRuntimeCoordinator> logger, 
        IOperationalStore? operationalStore = null,
        IEventBus? eventBus = null)
    {
        _sessionManager = sessionManager;
        _logger = logger;
        _operationalStore = operationalStore;
        _eventBus = eventBus;
    }

    public string? CurrentSessionId => _currentScope.Value?.SessionId;
    public UserContext? CurrentUserContext => _currentScope.Value?.UserContext;
    public string? CurrentAgentName => _currentScope.Value?.CurrentAgentName;
    public IReadOnlyCollection<string> CurrentAllowedTools => _currentScope.Value?.AllowedTools ?? Array.Empty<string>();
    public AgentExecutionStateMachine? CurrentStateMachine => _currentScope.Value?.StateMachine;

    public async Task<AgentStateTransition> TransitionStateAsync(AgentExecutionState newState, string trigger, string? detail = null, CancellationToken ct = default)
    {
        var sm = CurrentStateMachine ?? throw new InvalidOperationException("No execution scope is active.");
        var transition = sm.Transition(newState, trigger, CurrentAgentName, detail);
        
        await PublishEventAsync(new AgentStreamEvent
        {
            Type = AgentStreamEventType.StateTransition,
            AgentName = CurrentAgentName,
            Message = $"Transitioned from {transition.From} to {transition.To} via {trigger}",
            Data = new Dictionary<string, object>
            {
                ["from"] = transition.From.ToString(),
                ["to"] = transition.To.ToString(),
                ["trigger"] = trigger,
                ["detail"] = detail ?? string.Empty
            }
        }, ct);
        
        return transition;
    }

    public IDisposable BeginExecutionScope(string sessionId, UserContext context)
    {
        var previous = _currentScope.Value;
        _currentScope.Value = new RuntimeScope(sessionId, context, previous?.Writer);
        // Initialize state machine and transition to Idle
        _currentScope.Value.StateMachine.Transition(AgentExecutionState.Idle, "scope-started");
        return new ScopeHandle(_currentScope, previous);
    }

    public IDisposable BeginAgentScope(string agentName, IEnumerable<string>? allowedTools = null)
    {
        var scope = _currentScope.Value ?? throw new InvalidOperationException("No execution scope is active.");
        var previousAgent = scope.CurrentAgentName;
        var previousAllowed = scope.AllowedTools;

        scope.CurrentAgentName = agentName;
        scope.AllowedTools = allowedTools is null
            ? Array.Empty<string>()
            : allowedTools.Select(Normalize).Where(static value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

        return new AgentScopeHandle(scope, previousAgent, previousAllowed);
    }

    public async IAsyncEnumerable<AgentStreamEvent> StreamAsync(
        string sessionId,
        UserContext context,
        Func<CancellationToken, Task<AgentResponse>> operation,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var channel = Channel.CreateUnbounded<AgentStreamEvent>();

        var execution = Task.Run(async () =>
        {
            using var scope = BeginExecutionScope(sessionId, context);
            _currentScope.Value!.Writer = channel.Writer;

            await PublishEventAsync(new AgentStreamEvent
            {
                Type = AgentStreamEventType.SessionStarted,
                Message = "Agent runtime started"
            }, ct);

            try
            {
                var response = await operation(ct);

                await PublishEventAsync(new AgentStreamEvent
                {
                    Type = AgentStreamEventType.SessionCompleted,
                    AgentName = response.AgentName,
                    Message = response.Content,
                    IsTerminal = true,
                    Data = new Dictionary<string, object>
                    {
                        ["success"] = response.Success,
                        ["agentTier"] = response.AgentTier.ToString(),
                        ["actions"] = response.ActionsPerformed,
                        ["tools"] = response.ToolsUsed,
                        ["confidence"] = response.Confidence?.Value ?? 0d,
                        ["memoryInjected"] = context.Preferences.TryGetValue("memory_injected", out var mi) && mi is bool b && b
                    }
                }, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                await PublishEventAsync(new AgentStreamEvent
                {
                    Type = AgentStreamEventType.Error,
                    Message = "Streaming cancelled by caller",
                    IsTerminal = true
                }, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Runtime streaming failed for session {SessionId}", sessionId);
                await PublishEventAsync(new AgentStreamEvent
                {
                    Type = AgentStreamEventType.Error,
                    Message = ex.Message,
                    IsTerminal = true,
                    Data = new Dictionary<string, object>
                    {
                        ["exceptionType"] = ex.GetType().Name
                    }
                }, CancellationToken.None);
            }
            finally
            {
                channel.Writer.TryComplete();
            }
        }, CancellationToken.None);

        await foreach (var streamEvent in channel.Reader.ReadAllAsync(ct))
        {
            yield return streamEvent;
        }

        await execution;
    }

    public async Task PublishEventAsync(AgentStreamEvent streamEvent, CancellationToken ct = default)
    {
        var scope = _currentScope.Value;
        if (scope is not null)
        {
            streamEvent.SessionId = string.IsNullOrWhiteSpace(streamEvent.SessionId) ? scope.SessionId : streamEvent.SessionId;
            streamEvent.AgentName ??= scope.CurrentAgentName;
            streamEvent.Sequence = Interlocked.Increment(ref scope.Sequence);

            if (scope.Writer is not null)
            {
                await scope.Writer.WriteAsync(streamEvent, ct);
            }
        }

        if (PersistedEventTypes.Contains(streamEvent.Type) && !string.IsNullOrWhiteSpace(streamEvent.SessionId))
        {
            if (_eventBus != null)
            {
                await _eventBus.ExecuteInTransactionAsync(
                    async () => await PersistRuntimeEventAsync(streamEvent, ct),
                    new[] { streamEvent },
                    ct);
            }
            else
            {
                await PersistRuntimeEventAsync(streamEvent, ct);
            }
        }

        TrackMetrics(streamEvent);
    }

    public async Task RecordArtifactAsync(AgentExecutionArtifact artifact, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(artifact.SessionId))
        {
            artifact.SessionId = CurrentSessionId ?? string.Empty;
        }

        artifact.AgentName ??= CurrentAgentName;
        var list = _artifacts.GetOrAdd(artifact.SessionId, _ => new List<AgentExecutionArtifact>());
        lock (list)
        {
            list.Add(artifact);
        }

        var operation = async () =>
        {
            // Persist to operational store (durable)
            if (_operationalStore is not null)
            {
                try
                {
                    await _operationalStore.SaveArtifactAsync(artifact, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to persist artifact {ArtifactId} to operational store", artifact.Id);
                }
            }

            if (!string.IsNullOrWhiteSpace(artifact.SessionId))
            {
                var payload = JsonSerializer.Serialize(artifact.Data);
                await _sessionManager.AddEventAsync(artifact.SessionId, new AgentEvent
                {
                    Id = Guid.NewGuid().ToString("N"),
                    SessionId = artifact.SessionId,
                    AgentName = artifact.AgentName ?? "System",
                    AgentTier = AgentTier.Chief,
                    UserInput = "Artifact Recorded",
                    AgentResponse = $"Artifact '{artifact.Name}' of type {artifact.Type} saved.",
                    ActionsPerformed = { "RecordArtifact" },
                    Context = new Dictionary<string, object>
                    {
                        ["artifactId"] = artifact.Id,
                        ["artifactType"] = artifact.Type.ToString(),
                        ["summary"] = artifact.Summary ?? string.Empty
                    }
                });
            }
        };

        if (_eventBus != null)
        {
            await _eventBus.ExecuteInTransactionAsync(operation, new[] { artifact }, ct);
        }
        else
        {
            await operation();
        }

        await PublishEventAsync(new AgentStreamEvent
        {
            Type = AgentStreamEventType.ArtifactRecorded,
            AgentName = artifact.AgentName,
            Message = artifact.Name,
            Data = new Dictionary<string, object>
            {
                ["artifactId"] = artifact.Id,
                ["artifactType"] = artifact.Type.ToString(),
                ["status"] = artifact.Status
            }
        }, ct);
    }

    public async Task<IReadOnlyList<AgentExecutionArtifact>> GetArtifactsAsync(string sessionId, CancellationToken ct = default)
    {
        // Prefer durable store when available
        if (_operationalStore is not null)
        {
            try
            {
                return await _operationalStore.GetArtifactsAsync(sessionId, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read artifacts from operational store, falling back to in-memory");
            }
        }

        if (!_artifacts.TryGetValue(sessionId, out var list))
        {
            return Array.Empty<AgentExecutionArtifact>();
        }

        lock (list)
        {
            return list.OrderBy(item => item.CreatedAt).ToList();
        }
    }

    public async Task<AgentRuntimeMetricsSnapshot> GetMetricsAsync(string? sessionId = null, CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(sessionId) && _sessionMetrics.TryGetValue(sessionId, out var snapshot))
        {
            return Clone(snapshot);
        }

        // Try loading from store if session metrics not in memory
        if (!string.IsNullOrWhiteSpace(sessionId) && _operationalStore is not null)
        {
            try
            {
                var stored = await _operationalStore.GetLatestMetricsAsync(sessionId, ct);
                if (stored is not null) return stored;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read metrics from operational store");
            }
        }

        return Clone(_globalMetrics);
    }

    private async Task PersistRuntimeEventAsync(AgentStreamEvent streamEvent, CancellationToken ct)
    {
        await _sessionManager.AddEventAsync(streamEvent.SessionId, new AgentEvent
        {
            AgentName = streamEvent.AgentName ?? "AgentRuntime",
            AgentResponse = streamEvent.Message ?? string.Empty,
            Context = new Dictionary<string, object>
            {
                ["runtimeEventType"] = streamEvent.Type.ToString(),
                ["runtimeEventSequence"] = streamEvent.Sequence,
                ["runtimeEventPayload"] = JsonSerializer.Serialize(streamEvent.Data),
                ["runtimeEventTimestamp"] = streamEvent.Timestamp
            }
        });
    }

    private void TrackMetrics(AgentStreamEvent streamEvent)
    {
        UpdateMetrics(_globalMetrics, streamEvent);

        if (!string.IsNullOrWhiteSpace(streamEvent.SessionId))
        {
            var sessionMetrics = _sessionMetrics.GetOrAdd(streamEvent.SessionId, _ => new AgentRuntimeMetricsSnapshot { SessionId = streamEvent.SessionId });
            UpdateMetrics(sessionMetrics, streamEvent);

            // Flush snapshot to store on terminal events
            if (streamEvent.IsTerminal && _operationalStore is not null)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _operationalStore.SaveMetricsSnapshotAsync(Clone(sessionMetrics));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to flush metrics snapshot for session {SessionId}", streamEvent.SessionId);
                    }
                });
            }
        }
    }

    private static void UpdateMetrics(AgentRuntimeMetricsSnapshot metrics, AgentStreamEvent streamEvent)
    {
        metrics.UpdatedAt = DateTime.UtcNow;
        var key = streamEvent.Type.ToString();
        metrics.EventsByType[key] = metrics.EventsByType.TryGetValue(key, out var count) ? count + 1 : 1;

        if (!string.IsNullOrWhiteSpace(streamEvent.AgentName))
        {
            metrics.AgentExecutionCounts[streamEvent.AgentName] = metrics.AgentExecutionCounts.TryGetValue(streamEvent.AgentName, out var agentCount)
                ? agentCount + 1
                : 1;
        }

        switch (streamEvent.Type)
        {
            case AgentStreamEventType.SessionStarted:
                metrics.StreamCount++;
                _streamStartedCounter.Add(1, new KeyValuePair<string, object?>("sessionId", streamEvent.SessionId));
                break;
            case AgentStreamEventType.AgentSelected:
            case AgentStreamEventType.StepStarted:
                metrics.AgentExecutions++;
                _agentExecutionsCounter.Add(1, new KeyValuePair<string, object?>("agentName", streamEvent.AgentName));
                break;
            case AgentStreamEventType.ToolStarted:
                metrics.ToolExecutions++;
                _toolExecutionsCounter.Add(1, new KeyValuePair<string, object?>("agentName", streamEvent.AgentName));
                break;
            case AgentStreamEventType.ToolApprovalRequired:
                metrics.ToolApprovalsRequested++;
                break;
            case AgentStreamEventType.ToolApprovalResolved:
                metrics.ToolApprovalsResolved++;
                break;
            case AgentStreamEventType.FinalApprovalRequired:
                metrics.FinalApprovalsRequested++;
                break;
            case AgentStreamEventType.FinalApprovalResolved:
                metrics.FinalApprovalsResolved++;
                break;
            case AgentStreamEventType.HandoffStarted:
                metrics.Handoffs++;
                break;
            case AgentStreamEventType.RagStarted:
                metrics.RagQueries++;
                break;
            case AgentStreamEventType.ReviewCompleted:
                metrics.Reviews++;
                break;
            case AgentStreamEventType.StateTransition:
                break;
            case AgentStreamEventType.Error:
                _errorCounter.Add(1, new KeyValuePair<string, object?>("agentName", streamEvent.AgentName));
                if (streamEvent.Data.TryGetValue("fallback", out var fallback) && fallback is true)
                {
                    metrics.AgentFallbacks++;
                }
                break;
        }

        if (streamEvent.Data.TryGetValue("latencyMs", out var latencyValue)
            && double.TryParse(latencyValue?.ToString(), out var latencyMs))
        {
            if (streamEvent.Type == AgentStreamEventType.StepCompleted || streamEvent.Type == AgentStreamEventType.ReviewCompleted)
            {
                metrics.AverageAgentLatencyMs = UpdateAverage(metrics.AverageAgentLatencyMs, metrics.AgentExecutions, latencyMs);
            }
            else if (streamEvent.Type == AgentStreamEventType.ToolCompleted)
            {
                metrics.AverageToolLatencyMs = UpdateAverage(metrics.AverageToolLatencyMs, metrics.ToolExecutions, latencyMs);
            }
        }
    }

    private static double UpdateAverage(double currentAverage, long count, double newValue)
    {
        if (count <= 0)
        {
            return newValue;
        }

        return currentAverage + ((newValue - currentAverage) / count);
    }

    private static AgentRuntimeMetricsSnapshot Clone(AgentRuntimeMetricsSnapshot source)
    {
        return new AgentRuntimeMetricsSnapshot
        {
            SessionId = source.SessionId,
            UpdatedAt = source.UpdatedAt,
            StreamCount = source.StreamCount,
            AgentExecutions = source.AgentExecutions,
            AgentFallbacks = source.AgentFallbacks,
            ToolExecutions = source.ToolExecutions,
            ToolApprovalsRequested = source.ToolApprovalsRequested,
            ToolApprovalsResolved = source.ToolApprovalsResolved,
            FinalApprovalsRequested = source.FinalApprovalsRequested,
            FinalApprovalsResolved = source.FinalApprovalsResolved,
            Handoffs = source.Handoffs,
            RagQueries = source.RagQueries,
            Reviews = source.Reviews,
            AverageAgentLatencyMs = source.AverageAgentLatencyMs,
            AverageToolLatencyMs = source.AverageToolLatencyMs,
            EventsByType = new Dictionary<string, long>(source.EventsByType),
            AgentExecutionCounts = new Dictionary<string, long>(source.AgentExecutionCounts)
        };
    }

    private static string Normalize(string value) => value.Trim().ToLowerInvariant();

    private sealed class RuntimeScope
    {
        public RuntimeScope(string sessionId, UserContext userContext, ChannelWriter<AgentStreamEvent>? writer)
        {
            SessionId = sessionId;
            UserContext = userContext;
            Writer = writer;
            StateMachine = new AgentExecutionStateMachine { SessionId = sessionId };
        }

        public string SessionId { get; }
        public UserContext UserContext { get; }
        public ChannelWriter<AgentStreamEvent>? Writer { get; set; }
        public string? CurrentAgentName { get; set; }
        public IReadOnlyCollection<string> AllowedTools { get; set; } = Array.Empty<string>();
        public AgentExecutionStateMachine StateMachine { get; }
        public long Sequence;
    }

    private sealed class ScopeHandle : IDisposable
    {
        private readonly AsyncLocal<RuntimeScope?> _scope;
        private readonly RuntimeScope? _previous;

        public ScopeHandle(AsyncLocal<RuntimeScope?> scope, RuntimeScope? previous)
        {
            _scope = scope;
            _previous = previous;
        }

        public void Dispose()
        {
            _scope.Value = _previous;
        }
    }

    private sealed class AgentScopeHandle : IDisposable
    {
        private readonly RuntimeScope _scope;
        private readonly string? _previousAgent;
        private readonly IReadOnlyCollection<string> _previousAllowed;

        public AgentScopeHandle(RuntimeScope scope, string? previousAgent, IReadOnlyCollection<string> previousAllowed)
        {
            _scope = scope;
            _previousAgent = previousAgent;
            _previousAllowed = previousAllowed;
        }

        public void Dispose()
        {
            _scope.CurrentAgentName = _previousAgent;
            _scope.AllowedTools = _previousAllowed;
        }
    }
}