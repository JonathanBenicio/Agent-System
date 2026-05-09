using System.Collections.Concurrent;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Services;

/// <summary>
/// In-memory store for evaluation results. Development/testing fallback.
/// </summary>
public class InMemoryEvalResultStore : IEvalResultStore
{
    private readonly ConcurrentDictionary<string, List<EvalSuiteResult>> _results = new(StringComparer.OrdinalIgnoreCase);

    public Task SaveSuiteResultAsync(EvalSuiteResult result, CancellationToken ct = default)
    {
        var list = _results.GetOrAdd(result.AgentName, _ => []);
        lock (list)
        {
            list.Add(result);
        }
        return Task.CompletedTask;
    }

    public Task<EvalSuiteResult?> GetLatestBaselineAsync(string agentName, CancellationToken ct = default)
    {
        if (_results.TryGetValue(agentName, out var list))
        {
            lock (list)
            {
                var baseline = list.OrderByDescending(r => r.CompletedAt).FirstOrDefault();
                return Task.FromResult(baseline);
            }
        }
        return Task.FromResult<EvalSuiteResult?>(null);
    }

    public Task<IReadOnlyList<EvalSuiteResult>> GetHistoryAsync(string agentName, int limit = 10, CancellationToken ct = default)
    {
        if (_results.TryGetValue(agentName, out var list))
        {
            lock (list)
            {
                var history = list.OrderByDescending(r => r.CompletedAt).Take(limit).ToList();
                return Task.FromResult<IReadOnlyList<EvalSuiteResult>>(history);
            }
        }
        return Task.FromResult<IReadOnlyList<EvalSuiteResult>>([]);
    }
}
