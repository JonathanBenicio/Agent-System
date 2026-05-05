using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace AgenticSystem.Infrastructure.Persistence;

/// <summary>
/// Implementação PostgreSQL de ICostTracker — persiste custos e budgets em tabelas.
/// </summary>
public class PostgresCostTracker : ICostTracker
{
    private readonly string _connectionString;
    private readonly ILogger<PostgresCostTracker> _logger;
    private readonly decimal _defaultDailyBudget;

    public PostgresCostTracker(string connectionString, ILogger<PostgresCostTracker> logger, decimal defaultDailyBudget = 50.00m)
    {
        _connectionString = connectionString;
        _logger = logger;
        _defaultDailyBudget = defaultDailyBudget;
    }

    public void RecordCost(string serviceName, string category, decimal cost, string? tenantId = null)
    {
        var tid = tenantId ?? Tenant.DefaultTenantId;

        try
        {
            const string sql = """
                INSERT INTO cost_entries (service_name, category, tenant_id, cost, recorded_at)
                VALUES (@serviceName, @category, @tenantId, @cost, @recordedAt)
                """;

            using var conn = new NpgsqlConnection(_connectionString);
            conn.Open();

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("serviceName", serviceName);
            cmd.Parameters.AddWithValue("category", category);
            cmd.Parameters.AddWithValue("tenantId", tid);
            cmd.Parameters.AddWithValue("cost", cost);
            cmd.Parameters.AddWithValue("recordedAt", DateTime.UtcNow);

            cmd.ExecuteNonQuery();
        }
        catch (Exception ex) when (ex is NpgsqlException or TimeoutException)
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
            const string sql = """
                INSERT INTO cost_budgets (id, service_name, tenant_id, daily_budget, updated_at)
                VALUES (@id, @serviceName, @tenantId, @dailyBudget, @updatedAt)
                ON CONFLICT (id) DO UPDATE SET
                    daily_budget = EXCLUDED.daily_budget,
                    updated_at = EXCLUDED.updated_at
                """;

            using var conn = new NpgsqlConnection(_connectionString);
            conn.Open();

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("id", id);
            cmd.Parameters.AddWithValue("serviceName", serviceName);
            cmd.Parameters.AddWithValue("tenantId", tid);
            cmd.Parameters.AddWithValue("dailyBudget", dailyBudget);
            cmd.Parameters.AddWithValue("updatedAt", DateTime.UtcNow);

            cmd.ExecuteNonQuery();
        }
        catch (Exception ex) when (ex is NpgsqlException or TimeoutException)
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
            using var conn = new NpgsqlConnection(_connectionString);
            conn.Open();

            // Aggregate costs
            var costSql = """
                SELECT service_name, category, SUM(cost) as total_cost
                FROM cost_entries
                WHERE recorded_at >= @since
                """;

            if (tenantId is not null)
                costSql += " AND tenant_id = @tenantId";

            costSql += " GROUP BY service_name, category";

            using (var cmd = new NpgsqlCommand(costSql, conn))
            {
                cmd.Parameters.AddWithValue("since", since);
                if (tenantId is not null)
                    cmd.Parameters.AddWithValue("tenantId", tenantId);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    var svc = reader.GetString(0);
                    var cat = reader.GetString(1);
                    var cost = reader.GetDecimal(2);

                    costByService[svc] = costByService.GetValueOrDefault(svc) + cost;
                    costByCategory[cat] = costByCategory.GetValueOrDefault(cat) + cost;
                    total += cost;
                }
            }

            // Get budgets
            var budgetSql = "SELECT service_name, daily_budget FROM cost_budgets";
            if (tenantId is not null)
                budgetSql += " WHERE tenant_id = @tenantId";

            using (var cmd2 = new NpgsqlCommand(budgetSql, conn))
            {
                if (tenantId is not null)
                    cmd2.Parameters.AddWithValue("tenantId", tenantId);

                using var reader2 = cmd2.ExecuteReader();
                while (reader2.Read())
                    totalBudget += reader2.GetDecimal(1);
            }

            if (totalBudget == 0)
                totalBudget = _defaultDailyBudget;
        }
        catch (Exception ex) when (ex is NpgsqlException or TimeoutException)
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
            var sql = """
                SELECT COALESCE(SUM(cost), 0) FROM cost_entries
                WHERE service_name = @serviceName AND recorded_at >= @since
                """;

            if (tenantId is not null)
                sql += " AND tenant_id = @tenantId";

            using var conn = new NpgsqlConnection(_connectionString);
            conn.Open();

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("serviceName", serviceName);
            cmd.Parameters.AddWithValue("since", DateTime.UtcNow.Date);
            if (tenantId is not null)
                cmd.Parameters.AddWithValue("tenantId", tenantId);

            var result = cmd.ExecuteScalar();
            return result is decimal d ? d : 0;
        }
        catch (Exception ex) when (ex is NpgsqlException or TimeoutException)
        {
            _logger.LogError(ex, "Failed to get service cost for {Service}", serviceName);
            return 0;
        }
    }
}
