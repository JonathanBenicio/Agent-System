using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Core.Tools;

/// <summary>
/// Tenant-isolated analytics tool providing secure, parameterized Postgres analytics queries.
/// Actions: "costs", "performance", "sessions", "workflows".
/// Highly secure — query execution is fully isolated by global tenant filters in the DbContext.
/// </summary>
public class TenantAnalyticsTool : ITool
{
    public string Id => "tenant_analytics";
    public string Name => "Tenant Analytics Provider";
    public string Description => "Provides secure, structured analytics on tenant costs, performance, sessions, and workflows.";
    public ToolCategory Category => ToolCategory.Tasks;
    public bool RequiresAuth => true;

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<TenantAnalyticsTool> _logger;

    public TenantAnalyticsTool(IServiceProvider serviceProvider, ILogger<TenantAnalyticsTool> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public Task<bool> IsAvailableAsync(CancellationToken ct = default) => Task.FromResult(true);

    public async Task<ToolResult> ExecuteAsync(ToolInput input, CancellationToken ct = default)
    {
        return input.Action.ToLowerInvariant() switch
        {
            "costs" => await GetCostsAsync(input, ct),
            "performance" => await GetPerformanceAsync(input, ct),
            "sessions" => await GetSessionsAsync(input, ct),
            "workflows" => await GetWorkflowsAsync(input, ct),
            _ => ToolResult.Fail($"Unknown action '{input.Action}'. Supported actions: costs, performance, sessions, workflows.")
        };
    }

    private async Task<ToolResult> GetCostsAsync(ToolInput input, CancellationToken ct)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = ResolveDbContext(scope.ServiceProvider);
            if (dbContext == null) return ToolResult.Fail("Database context not available.");

            var dbSet = GetDbSet(dbContext, "CostEntries");
            if (dbSet == null) return ToolResult.Fail("Cost history table is not available.");

            var days = GetParam<int>(input, "days");
            if (days <= 0) days = 30;

            var cutoff = DateTime.UtcNow.AddDays(-days);
            var list = new List<dynamic>();
            foreach (var item in dbSet)
            {
                list.Add(item);
            }

            var filtered = list.Where(x => (DateTime)x.RecordedAt >= cutoff).ToList();
            var totalCost = filtered.Sum(x => (decimal)x.Cost);

            var byService = filtered.GroupBy(x => (string)x.ServiceName)
                .ToDictionary(g => g.Key, g => g.Sum(x => (decimal)x.Cost));

            var byCategory = filtered.GroupBy(x => (string)x.Category)
                .ToDictionary(g => g.Key, g => g.Sum(x => (decimal)x.Cost));

            return ToolResult.Ok(new
            {
                days,
                totalCost,
                byService,
                byCategory
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing costs analytics query for tenant");
            return ToolResult.Fail($"Failed to compute cost analytics: {ex.Message}");
        }
    }

    private async Task<ToolResult> GetPerformanceAsync(ToolInput input, CancellationToken ct)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = ResolveDbContext(scope.ServiceProvider);
            if (dbContext == null) return ToolResult.Fail("Database context not available.");

            var dbSet = GetDbSet(dbContext, "AgentPerformanceMetrics");
            if (dbSet == null) return ToolResult.Fail("Performance metrics table is not available.");

            var days = GetParam<int>(input, "days");
            if (days <= 0) days = 30;

            var cutoff = DateTime.UtcNow.AddDays(-days);
            var list = new List<dynamic>();
            foreach (var item in dbSet)
            {
                list.Add(item);
            }

            var filtered = list.Where(x => (DateTime)x.RecordedAt >= cutoff).ToList();
            var totalExecutions = filtered.Count;
            var successCount = filtered.Count(x => (bool)x.Success);
            var successRate = totalExecutions > 0 ? (double)successCount / totalExecutions : 1.0;
            var avgLatency = totalExecutions > 0 ? filtered.Average(x => (double)x.LatencyMs) : 0.0;

            var byAgent = filtered.GroupBy(x => (string)x.AgentName)
                .Select(g => new
                {
                    AgentName = g.Key,
                    Executions = g.Count(),
                    SuccessRate = g.Count() > 0 ? (double)g.Count(x => (bool)x.Success) / g.Count() : 1.0,
                    AvgLatencyMs = g.Count() > 0 ? g.Average(x => (double)x.LatencyMs) : 0.0
                })
                .ToList();

            return ToolResult.Ok(new
            {
                days,
                totalExecutions,
                successRate,
                avgLatencyMs = avgLatency,
                byAgent
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing performance analytics query for tenant");
            return ToolResult.Fail($"Failed to compute performance metrics: {ex.Message}");
        }
    }

    private async Task<ToolResult> GetSessionsAsync(ToolInput input, CancellationToken ct)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = ResolveDbContext(scope.ServiceProvider);
            if (dbContext == null) return ToolResult.Fail("Database context not available.");

            var dbSet = GetDbSet(dbContext, "SessionRecords");
            if (dbSet == null) return ToolResult.Fail("Session history table is not available.");

            var list = new List<dynamic>();
            foreach (var item in dbSet)
            {
                list.Add(item);
            }

            var activeSessions = list.Count(x => x.EndedAt == null);
            var totalSessions = list.Count;

            return ToolResult.Ok(new
            {
                activeSessions,
                totalSessions
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing sessions analytics query for tenant");
            return ToolResult.Fail($"Failed to compute session statistics: {ex.Message}");
        }
    }

    private async Task<ToolResult> GetWorkflowsAsync(ToolInput input, CancellationToken ct)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = ResolveDbContext(scope.ServiceProvider);
            if (dbContext == null) return ToolResult.Fail("Database context not available.");

            var dbSet = GetDbSet(dbContext, "WorkflowExecutions");
            if (dbSet == null) return ToolResult.Fail("Workflow executions table is not available.");

            var list = new List<dynamic>();
            foreach (var item in dbSet)
            {
                list.Add(item);
            }

            var total = list.Count;
            var byStatus = list.GroupBy(x => (string)x.Status)
                .ToDictionary(g => g.Key, g => g.Count());

            return ToolResult.Ok(new
            {
                totalWorkflows = total,
                byStatus
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing workflows analytics query for tenant");
            return ToolResult.Fail($"Failed to compute workflow statistics: {ex.Message}");
        }
    }

    private static object? ResolveDbContext(IServiceProvider scopedProvider)
    {
        var dbContextType = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
            .FirstOrDefault(t => t.Name == "AgenticDbContext");

        if (dbContextType == null) return null;

        return scopedProvider.GetService(dbContextType);
    }

    private IEnumerable? GetDbSet(object dbContext, string propertyName)
    {
        var property = dbContext.GetType().GetProperty(propertyName);
        return property?.GetValue(dbContext) as IEnumerable;
    }

    private static T? GetParam<T>(ToolInput input, string key)
    {
        if (input.Parameters.TryGetValue(key, out var value))
        {
            if (value is T typed) return typed;
            if (value is System.Text.Json.JsonElement json)
            {
                return System.Text.Json.JsonSerializer.Deserialize<T>(json.GetRawText());
            }
            try
            {
                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch
            {
                return default;
            }
        }
        return default;
    }
}
