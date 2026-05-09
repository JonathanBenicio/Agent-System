using AgenticSystem.Core.Interfaces;

namespace AgenticSystem.Core.Interfaces;

/// <summary>
/// Persistence store for model performance metrics.
/// </summary>
public interface IModelPerformanceStore
{
    Task RecordPerformanceAsync(string modelId, ModelPerformanceRecord record, CancellationToken ct = default);
    Task<IReadOnlyList<ModelPerformanceRecord>> GetPerformanceHistoryAsync(string modelId, int limit = 100, CancellationToken ct = default);
    Task<Dictionary<string, ModelPerformanceSummary>> GetAggregatedMetricsAsync(CancellationToken ct = default);
}

public class ModelPerformanceSummary
{
    public double AverageLatencyMs { get; set; }
    public double SuccessRate { get; set; }
    public double TotalCostUsd { get; set; }
    public long TotalTokens { get; set; }
    public int SampleCount { get; set; }
}
