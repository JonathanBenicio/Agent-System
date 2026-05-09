using System.Collections.Concurrent;
using AgenticSystem.Core.Interfaces;

namespace AgenticSystem.Core.Services;

public class InMemoryModelPerformanceStore : IModelPerformanceStore
{
    private readonly ConcurrentDictionary<string, ConcurrentQueue<ModelPerformanceRecord>> _history = new();

    public Task RecordPerformanceAsync(string modelId, ModelPerformanceRecord record, CancellationToken ct = default)
    {
        var queue = _history.GetOrAdd(modelId, _ => new ConcurrentQueue<ModelPerformanceRecord>());
        queue.Enqueue(record);
        
        // Keep only last 100
        while (queue.Count > 100) queue.TryDequeue(out _);
        
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ModelPerformanceRecord>> GetPerformanceHistoryAsync(string modelId, int limit = 100, CancellationToken ct = default)
    {
        if (_history.TryGetValue(modelId, out var queue))
        {
            return Task.FromResult<IReadOnlyList<ModelPerformanceRecord>>(queue.Reverse().Take(limit).ToList());
        }
        return Task.FromResult<IReadOnlyList<ModelPerformanceRecord>>(new List<ModelPerformanceRecord>());
    }

    public Task<Dictionary<string, ModelPerformanceSummary>> GetAggregatedMetricsAsync(CancellationToken ct = default)
    {
        var summary = _history.ToDictionary(
            kvp => kvp.Key,
            kvp => new ModelPerformanceSummary
            {
                AverageLatencyMs = kvp.Value.Any() ? kvp.Value.Average(r => r.LatencyMs) : 0,
                SuccessRate = kvp.Value.Any() ? (double)kvp.Value.Count(r => r.Success) / kvp.Value.Count : 0,
                TotalCostUsd = kvp.Value.Sum(r => r.ActualCostUsd),
                TotalTokens = kvp.Value.Sum(r => (long)r.InputTokens + r.OutputTokens),
                SampleCount = kvp.Value.Count
            });

        return Task.FromResult(summary);
    }
}
