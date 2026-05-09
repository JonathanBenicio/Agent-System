using AgenticSystem.Core.Interfaces;
using AgenticSystem.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Infrastructure.Persistence;

public class PostgresModelPerformanceStore : IModelPerformanceStore
{
    private readonly IDbContextFactory<AgenticDbContext> _dbContextFactory;
    private readonly ILogger<PostgresModelPerformanceStore> _logger;

    public PostgresModelPerformanceStore(
        IDbContextFactory<AgenticDbContext> dbContextFactory,
        ILogger<PostgresModelPerformanceStore> logger)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    public async Task RecordPerformanceAsync(string modelId, ModelPerformanceRecord record, CancellationToken ct = default)
    {
        using var context = await _dbContextFactory.CreateDbContextAsync(ct);
        
        var entity = new ModelPerformanceEntity
        {
            Id = Guid.NewGuid().ToString("N"),
            ModelId = modelId,
            LatencyMs = record.LatencyMs,
            Success = record.Success,
            InputTokens = record.InputTokens,
            OutputTokens = record.OutputTokens,
            ActualCostUsd = record.ActualCostUsd,
            RecordedAt = record.RecordedAt
        };

        context.ModelPerformanceRecords.Add(entity);
        await context.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<ModelPerformanceRecord>> GetPerformanceHistoryAsync(string modelId, int limit = 100, CancellationToken ct = default)
    {
        using var context = await _dbContextFactory.CreateDbContextAsync(ct);
        
        var entities = await context.ModelPerformanceRecords
            .Where(e => e.ModelId == modelId)
            .OrderByDescending(e => e.RecordedAt)
            .Take(limit)
            .ToListAsync(ct);

        return entities.Select(e => new ModelPerformanceRecord
        {
            LatencyMs = e.LatencyMs,
            Success = e.Success,
            InputTokens = e.InputTokens,
            OutputTokens = e.OutputTokens,
            ActualCostUsd = e.ActualCostUsd,
            RecordedAt = e.RecordedAt
        }).ToList();
    }

    public async Task<Dictionary<string, ModelPerformanceSummary>> GetAggregatedMetricsAsync(CancellationToken ct = default)
    {
        using var context = await _dbContextFactory.CreateDbContextAsync(ct);
        
        // Note: Simple aggregation for now. 
        // In a high-volume scenario, we'd use a dedicated aggregate table or materialized view.
        var metrics = await context.ModelPerformanceRecords
            .GroupBy(e => e.ModelId)
            .Select(g => new
            {
                ModelId = g.Key,
                AvgLatency = g.Average(e => e.LatencyMs),
                SuccessCount = g.Count(e => e.Success),
                TotalCount = g.Count(),
                TotalCost = g.Sum(e => e.ActualCostUsd),
                TotalTokens = g.Sum(e => (long)e.InputTokens + e.OutputTokens)
            })
            .ToListAsync(ct);

        return metrics.ToDictionary(
            m => m.ModelId,
            m => new ModelPerformanceSummary
            {
                AverageLatencyMs = m.AvgLatency,
                SuccessRate = (double)m.SuccessCount / m.TotalCount,
                TotalCostUsd = m.TotalCost,
                TotalTokens = m.TotalTokens,
                SampleCount = m.TotalCount
            });
    }
}
