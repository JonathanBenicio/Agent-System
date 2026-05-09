using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Infrastructure.Persistence;

/// <summary>
/// Decorator para ISmartRouter — write-through cache com warm-up do PostgreSQL.
/// Delega roteamento ao inner e persiste métricas de performance.
/// </summary>
public class PersistentSmartRouter : ISmartRouter
{
    private readonly ISmartRouter _inner;
    private readonly IDbContextFactory<AgenticDbContext> _dbContextFactory;
    private readonly ILogger<PersistentSmartRouter> _logger;
    private readonly SemaphoreSlim _warmupLock = new(1, 1);
    private volatile bool _warmedUp;

    public PersistentSmartRouter(
        ISmartRouter inner,
        IDbContextFactory<AgenticDbContext> dbContextFactory,
        ILogger<PersistentSmartRouter> logger)
    {
        _inner = inner;
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    public async Task<RoutingDecision> RouteAsync(AnalysisResult analysis, UserContext context)
    {
        await EnsureWarmedUpAsync();
        return await _inner.RouteAsync(analysis, context);
    }

    public async Task RecordPerformanceAsync(string agentName, AgentPerformanceMetric metric)
    {
        // Write-through: inner (in-memory) + persist
        await _inner.RecordPerformanceAsync(agentName, metric);
        await PersistMetricAsync(agentName, metric);
    }

    public async Task<IEnumerable<AgentRanking>> GetRankingsByDomainAsync(string domain)
    {
        await EnsureWarmedUpAsync();
        return await _inner.GetRankingsByDomainAsync(domain);
    }

    public Task<ProviderRoutingDecision> RouteProviderAsync(string? requestedProvider, string? requestedModel)
    {
        return _inner.RouteProviderAsync(requestedProvider, requestedModel);
    }

    private async Task PersistMetricAsync(string agentName, AgentPerformanceMetric metric)
    {
        try
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync();
            db.AgentPerformanceMetrics.Add(new AgentPerformanceMetricEntity
            {
                AgentName = agentName,
                Domain = metric.Domain,
                LatencyMs = metric.Latency.TotalMilliseconds,
                Success = metric.Success,
                UserSatisfaction = metric.UserSatisfaction,
                RecordedAt = metric.RecordedAt
            });

            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist performance metric for {Agent} - in-memory still recorded", agentName);
        }
    }

    private async Task EnsureWarmedUpAsync()
    {
        if (_warmedUp)
        {
            return;
        }

        await _warmupLock.WaitAsync();
        try
        {
            if (_warmedUp)
            {
                return;
            }

            await using var db = await _dbContextFactory.CreateDbContextAsync();
            var entities = await db.AgentPerformanceMetrics
                .AsNoTracking()
                .Where(metric => metric.RecordedAt >= DateTime.UtcNow.AddDays(-7))
                .OrderByDescending(metric => metric.RecordedAt)
                .ToListAsync();

            var count = 0;
            foreach (var entity in entities)
            {
                var metric = new AgentPerformanceMetric
                {
                    Domain = entity.Domain,
                    Latency = TimeSpan.FromMilliseconds(entity.LatencyMs),
                    Success = entity.Success,
                    UserSatisfaction = entity.UserSatisfaction,
                    RecordedAt = entity.RecordedAt
                };

                await _inner.RecordPerformanceAsync(entity.AgentName, metric);
                count++;
            }

            _warmedUp = true;
            _logger.LogInformation("SmartRouter warmed up with {Count} metrics from PostgreSQL via EF Core", count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to warm up SmartRouter from PostgreSQL - starting cold");
            _warmedUp = true;
        }
        finally
        {
            _warmupLock.Release();
        }
    }
}
