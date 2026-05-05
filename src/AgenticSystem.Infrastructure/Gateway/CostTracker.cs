using System.Collections.Concurrent;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Infrastructure.Gateway;

/// <summary>
/// Rastreamento de custos por serviço, categoria e tenant (implementação in-memory).
/// </summary>
public class CostTracker : ICostTracker
{
    private readonly ConcurrentDictionary<string, ServiceCostData> _serviceCosts = new();
    private readonly decimal _defaultDailyBudget;

    public CostTracker(decimal defaultDailyBudget = 50.00m)
    {
        _defaultDailyBudget = defaultDailyBudget;
    }

    public void RecordCost(string serviceName, string category, decimal cost, string? tenantId = null)
    {
        var key = BuildKey(serviceName, tenantId);
        var data = _serviceCosts.GetOrAdd(key, _ => new ServiceCostData
        {
            ServiceName = serviceName,
            Category = category,
            TenantId = tenantId ?? Tenant.DefaultTenantId,
            DailyBudget = _defaultDailyBudget
        });

        data.AddCost(cost);
    }

    public void SetBudget(string serviceName, decimal dailyBudget, string? tenantId = null)
    {
        var key = BuildKey(serviceName, tenantId);
        var data = _serviceCosts.GetOrAdd(key, _ => new ServiceCostData
        {
            ServiceName = serviceName,
            TenantId = tenantId ?? Tenant.DefaultTenantId,
            DailyBudget = dailyBudget
        });
        data.DailyBudget = dailyBudget;
    }

    public CostReport GetReport(TimeSpan? range = null, string? tenantId = null)
    {
        var costByService = new Dictionary<string, decimal>();
        var costByCategory = new Dictionary<string, decimal>();
        decimal total = 0;
        decimal totalBudget = 0;

        var entries = tenantId is null
            ? _serviceCosts
            : _serviceCosts.Where(kvp => kvp.Value.TenantId == tenantId);

        foreach (var kvp in entries)
        {
            var data = kvp.Value;
            data.PrunePastDay();
            var cost = data.GetDailyCost();
            costByService[data.ServiceName] = costByService.GetValueOrDefault(data.ServiceName) + cost;

            if (!costByCategory.ContainsKey(data.Category))
                costByCategory[data.Category] = 0;
            costByCategory[data.Category] += cost;

            total += cost;
            totalBudget += data.DailyBudget;
        }

        return new CostReport
        {
            TotalCost = total,
            DailyBudget = totalBudget,
            UsagePercent = totalBudget > 0 ? (double)(total / totalBudget * 100) : 0,
            CostByService = costByService,
            CostByCategory = costByCategory,
            PeriodStart = DateTime.UtcNow.Date,
            PeriodEnd = DateTime.UtcNow,
            BudgetAlert = totalBudget > 0 && total >= totalBudget * 0.8m
        };
    }

    public decimal GetServiceCost(string serviceName, string? tenantId = null)
    {
        var key = BuildKey(serviceName, tenantId);
        if (_serviceCosts.TryGetValue(key, out var data))
        {
            data.PrunePastDay();
            return data.GetDailyCost();
        }
        return 0;
    }

    private static string BuildKey(string serviceName, string? tenantId)
        => tenantId is null ? serviceName : $"{tenantId}:{serviceName}";

    private class ServiceCostData
    {
        public string ServiceName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string TenantId { get; set; } = Tenant.DefaultTenantId;
        public decimal DailyBudget { get; set; }
        private readonly ConcurrentQueue<(DateTime Time, decimal Cost)> _entries = new();

        public void AddCost(decimal cost) => _entries.Enqueue((DateTime.UtcNow, cost));

        public decimal GetDailyCost()
        {
            var today = DateTime.UtcNow.Date;
            return _entries.Where(e => e.Time >= today).Sum(e => e.Cost);
        }

        public void PrunePastDay()
        {
            var cutoff = DateTime.UtcNow.AddDays(-1);
            while (_entries.TryPeek(out var entry) && entry.Time < cutoff)
                _entries.TryDequeue(out _);
        }
    }
}
