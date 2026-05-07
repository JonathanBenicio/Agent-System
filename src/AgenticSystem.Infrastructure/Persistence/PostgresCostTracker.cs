using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Infrastructure.Persistence;

/// <summary>
/// Implementação PostgreSQL de ICostTracker — persiste custos e budgets em tabelas.
/// </summary>
public class PostgresCostTracker : ICostTracker
{
    private readonly IDbContextFactory<AgenticDbContext> _dbContextFactory;
    private readonly ILogger<PostgresCostTracker> _logger;
    private readonly decimal _defaultDailyBudget;

    public PostgresCostTracker(IDbContextFactory<AgenticDbContext> dbContextFactory, ILogger<PostgresCostTracker> logger, decimal defaultDailyBudget = 50.00m)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
        _defaultDailyBudget = defaultDailyBudget;
    }

    public void RecordCost(string serviceName, string category, decimal cost, string? tenantId = null)
    {
        var tid = tenantId ?? Tenant.DefaultTenantId;

        try
        {
            using var db = _dbContextFactory.CreateDbContext();
            db.CostEntries.Add(new CostEntryEntity
            {
                ServiceName = serviceName,
                Category = category,
                TenantId = tid,
                Cost = cost,
                RecordedAt = DateTime.UtcNow
            });
            db.SaveChanges();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record cost for service {Service}", serviceName);
        }
    }

    public void SetBudget(string serviceName, decimal dailyBudget, string? tenantId = null)
    {
        var tid = tenantId ?? Tenant.DefaultTenantId;
        var id = $"{tid}:{serviceName}";

        try
        {
            using var db = _dbContextFactory.CreateDbContext();
            var budget = db.CostBudgets.FirstOrDefault(item => item.Id == id);
            if (budget is null)
            {
                db.CostBudgets.Add(new CostBudgetEntity
                {
                    Id = id,
                    ServiceName = serviceName,
                    TenantId = tid,
                    DailyBudget = dailyBudget,
                    UpdatedAt = DateTime.UtcNow
                });
            }
            else
            {
                budget.ServiceName = serviceName;
                budget.TenantId = tid;
                budget.DailyBudget = dailyBudget;
                budget.UpdatedAt = DateTime.UtcNow;
            }

            db.SaveChanges();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set budget for service {Service}", serviceName);
        }
    }

    public CostReport GetReport(TimeSpan? range = null, string? tenantId = null)
    {
        var costByService = new Dictionary<string, decimal>();
        var costByCategory = new Dictionary<string, decimal>();
        decimal total = 0;
        decimal totalBudget = 0;
        var since = DateTime.UtcNow.Date;
        if (range.HasValue)
            since = DateTime.UtcNow - range.Value;

        try
        {
            using var db = _dbContextFactory.CreateDbContext();
            var costsQuery = db.CostEntries.AsNoTracking().Where(entry => entry.RecordedAt >= since);
            if (!string.IsNullOrWhiteSpace(tenantId))
            {
                costsQuery = costsQuery.Where(entry => entry.TenantId == tenantId);
            }

            var aggregated = costsQuery
                .GroupBy(entry => new { entry.ServiceName, entry.Category })
                .Select(group => new
                {
                    group.Key.ServiceName,
                    group.Key.Category,
                    Total = group.Sum(item => item.Cost)
                })
                .ToList();

            foreach (var item in aggregated)
            {
                costByService[item.ServiceName] = costByService.GetValueOrDefault(item.ServiceName) + item.Total;
                costByCategory[item.Category] = costByCategory.GetValueOrDefault(item.Category) + item.Total;
                total += item.Total;
            }

            var budgetsQuery = db.CostBudgets.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(tenantId))
            {
                budgetsQuery = budgetsQuery.Where(budget => budget.TenantId == tenantId);
            }

            totalBudget = budgetsQuery.Sum(budget => (decimal?)budget.DailyBudget) ?? 0m;

            if (totalBudget == 0)
                totalBudget = _defaultDailyBudget;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get cost report");
        }

        return new CostReport
        {
            TotalCost = total,
            DailyBudget = totalBudget,
            UsagePercent = totalBudget > 0 ? (double)(total / totalBudget * 100) : 0,
            CostByService = costByService,
            CostByCategory = costByCategory,
            PeriodStart = since,
            PeriodEnd = DateTime.UtcNow,
            BudgetAlert = totalBudget > 0 && total >= totalBudget * 0.8m
        };
    }

    public decimal GetServiceCost(string serviceName, string? tenantId = null)
    {
        try
        {
            using var db = _dbContextFactory.CreateDbContext();
            var query = db.CostEntries.AsNoTracking()
                .Where(entry => entry.ServiceName == serviceName)
                .Where(entry => entry.RecordedAt >= DateTime.UtcNow.Date);

            if (!string.IsNullOrWhiteSpace(tenantId))
            {
                query = query.Where(entry => entry.TenantId == tenantId);
            }

            return query.Sum(entry => (decimal?)entry.Cost) ?? 0m;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get service cost for {Service}", serviceName);
            return 0;
        }
    }
}
