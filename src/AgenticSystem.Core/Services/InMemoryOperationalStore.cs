using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using System.Collections.Concurrent;

namespace AgenticSystem.Core.Services;

/// <summary>
/// In-memory fallback for IOperationalStore when PostgreSQL is not configured.
/// </summary>
public class InMemoryOperationalStore : IOperationalStore
{
    private readonly ConcurrentDictionary<string, SystemState> _systemStates = new();
    private readonly List<Reflection> _reflections = new();
    private readonly List<AgentExecutionArtifact> _artifacts = new();
    private readonly List<AgentRuntimeMetricsSnapshot> _metrics = new();
    private readonly List<RuntimeEvaluationResult> _evaluations = new();

    public Task SaveArtifactAsync(AgentExecutionArtifact artifact, CancellationToken ct = default)
    {
        lock (_artifacts)
        {
            _artifacts.Add(artifact);
        }
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<AgentExecutionArtifact>> GetArtifactsAsync(string sessionId, CancellationToken ct = default)
    {
        lock (_artifacts)
        {
            var result = _artifacts.Where(a => a.SessionId == sessionId).ToList();
            return Task.FromResult<IReadOnlyList<AgentExecutionArtifact>>(result);
        }
    }

    public Task<IReadOnlyList<AgentExecutionArtifact>> QueryArtifactsAsync(
        string? sessionId = null,
        AgentExecutionArtifactType? type = null,
        DateTime? from = null,
        DateTime? to = null,
        int limit = 100,
        CancellationToken ct = default)
    {
        lock (_artifacts)
        {
            var query = _artifacts.AsEnumerable();
            if (sessionId != null) query = query.Where(a => a.SessionId == sessionId);
            if (type != null) query = query.Where(a => a.Type == type);
            if (from != null) query = query.Where(a => a.CreatedAt >= from);
            if (to != null) query = query.Where(a => a.CreatedAt <= to);
            
            var result = query.Take(limit).ToList();
            return Task.FromResult<IReadOnlyList<AgentExecutionArtifact>>(result);
        }
    }

    public Task SaveMetricsSnapshotAsync(AgentRuntimeMetricsSnapshot snapshot, CancellationToken ct = default)
    {
        lock (_metrics)
        {
            _metrics.Add(snapshot);
        }
        return Task.CompletedTask;
    }

    public Task<AgentRuntimeMetricsSnapshot?> GetLatestMetricsAsync(string? sessionId = null, CancellationToken ct = default)
    {
        lock (_metrics)
        {
            var query = _metrics.AsEnumerable();
            if (sessionId != null) query = query.Where(m => m.SessionId == sessionId);
            return Task.FromResult(query.OrderByDescending(m => m.UpdatedAt).FirstOrDefault());
        }
    }

    public Task<IReadOnlyList<AgentRuntimeMetricsSnapshot>> GetMetricsHistoryAsync(
        string? sessionId = null,
        DateTime? from = null,
        DateTime? to = null,
        int limit = 100,
        CancellationToken ct = default)
    {
        lock (_metrics)
        {
            var query = _metrics.AsEnumerable();
            if (sessionId != null) query = query.Where(m => m.SessionId == sessionId);
            if (from != null) query = query.Where(m => m.UpdatedAt >= from);
            if (to != null) query = query.Where(m => m.UpdatedAt <= to);
            
            var result = query.OrderByDescending(m => m.UpdatedAt).Take(limit).ToList();
            return Task.FromResult<IReadOnlyList<AgentRuntimeMetricsSnapshot>>(result);
        }
    }

    public Task SaveReflectionAsync(Reflection reflection, CancellationToken ct = default)
    {
        lock (_reflections)
        {
            _reflections.Add(reflection);
        }
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Reflection>> GetReflectionsAsync(string sessionId, CancellationToken ct = default)
    {
        lock (_reflections)
        {
            var result = _reflections.Where(r => r.SessionId == sessionId).ToList();
            return Task.FromResult<IReadOnlyList<Reflection>>(result);
        }
    }

    public Task<IReadOnlyList<Reflection>> GetRecentLearningsAsync(int count = 10, CancellationToken ct = default)
    {
        lock (_reflections)
        {
            var result = _reflections.OrderByDescending(r => r.CreatedAt).Take(count).ToList();
            return Task.FromResult<IReadOnlyList<Reflection>>(result);
        }
    }

    public Task<IReadOnlyList<Reflection>> GetReflectionsSinceAsync(string? lastReflectionId, int limit = 100, CancellationToken ct = default)
    {
        lock (_reflections)
        {
            var query = _reflections.AsEnumerable();
            if (!string.IsNullOrEmpty(lastReflectionId))
            {
                var index = _reflections.FindIndex(r => r.Id == lastReflectionId);
                if (index >= 0)
                {
                    query = _reflections.Skip(index + 1);
                }
            }
            
            var result = query.Take(limit).ToList();
            return Task.FromResult<IReadOnlyList<Reflection>>(result);
        }
    }

    public Task SaveEvaluationAsync(RuntimeEvaluationResult evaluation, CancellationToken ct = default)
    {
        lock (_evaluations)
        {
            _evaluations.Add(evaluation);
        }
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<RuntimeEvaluationResult>> GetEvaluationsAsync(
        string? sessionId = null,
        string? agentName = null,
        DateTime? from = null,
        DateTime? to = null,
        int limit = 50,
        CancellationToken ct = default)
    {
        lock (_evaluations)
        {
            var query = _evaluations.AsEnumerable();
            if (sessionId != null) query = query.Where(e => e.SessionId == sessionId);
            if (agentName != null) query = query.Where(e => e.AgentName == agentName);
            if (from != null) query = query.Where(e => e.CreatedAt >= from);
            if (to != null) query = query.Where(e => e.CreatedAt <= to);
            
            var result = query.OrderByDescending(e => e.CreatedAt).Take(limit).ToList();
            return Task.FromResult<IReadOnlyList<RuntimeEvaluationResult>>(result);
        }
    }

    public Task<RuntimeEvaluationResult?> GetLatestEvaluationAsync(string? agentName = null, CancellationToken ct = default)
    {
        lock (_evaluations)
        {
            var query = _evaluations.AsEnumerable();
            if (agentName != null) query = query.Where(e => e.AgentName == agentName);
            return Task.FromResult(query.OrderByDescending(e => e.CreatedAt).FirstOrDefault());
        }
    }

    public Task<SystemState?> GetSystemStateAsync(string id, CancellationToken ct = default)
    {
        _systemStates.TryGetValue(id, out var state);
        return Task.FromResult(state);
    }

    public Task SaveSystemStateAsync(SystemState state, CancellationToken ct = default)
    {
        _systemStates[state.Id] = state;
        return Task.CompletedTask;
    }
}
