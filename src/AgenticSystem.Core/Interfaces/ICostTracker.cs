using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Interfaces;

/// <summary>
/// Rastreamento de custos por serviço, categoria e tenant.
/// </summary>
public interface ICostTracker
{
    void RecordCost(string serviceName, string category, decimal cost, string? tenantId = null);
    void SetBudget(string serviceName, decimal dailyBudget, string? tenantId = null);
    CostReport GetReport(TimeSpan? range = null, string? tenantId = null);
    decimal GetServiceCost(string serviceName, string? tenantId = null);
}
