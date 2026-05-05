using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace AgenticSystem.Infrastructure.Persistence;

/// <summary>
/// Decorator para ISmartRouter — write-through cache com warm-up do PostgreSQL.
/// Delega roteamento ao inner e persiste métricas de performance.
/// </summary>
public class PersistentSmartRouter : ISmartRouter
{
    private readonly ISmartRouter _inner;
    private readonly string _connectionString;
    private readonly ILogger<PersistentSmartRouter> _logger;
    private readonly SemaphoreSlim _warmupLock = new(1, 1);
    private volatile bool _warmedUp;

    public PersistentSmartRouter(
        ISmartRouter inner,
        string connectionString,
        ILogger<PersistentSmartRouter> logger)
    {
        _inner = inner;
        _connectionString = connectionString;
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

    private async Task PersistMetricAsync(string agentName, AgentPerformanceMetric metric)
    {
        try
        {
            const string sql = """
                INSERT INTO agent_performance_metrics (agent_name, domain, latency_ms, success, user_satisfaction, recorded_at)
                VALUES (@agentName, @domain, @latencyMs, @success, @userSatisfaction, @recordedAt)
                """;

            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("agentName", agentName);
            cmd.Parameters.AddWithValue("domain", metric.Domain);
            cmd.Parameters.AddWithValue("latencyMs", metric.Latency.TotalMilliseconds);
            cmd.Parameters.AddWithValue("success", metric.Success);
            cmd.Parameters.AddWithValue("userSatisfaction", (object?)metric.UserSatisfaction ?? DBNull.Value);
            cmd.Parameters.AddWithValue("recordedAt", metric.RecordedAt);

            await cmd.ExecuteNonQueryAsync();
        }
        catch (Exception ex) when (ex is NpgsqlException or TimeoutException)
        {
            _logger.LogWarning(ex, "Failed to persist performance metric for {Agent} — in-memory still recorded", agentName);
        }
    }

    private async Task EnsureWarmedUpAsync()
    {
        if (_warmedUp) return;

        await _warmupLock.WaitAsync();
        try
        {
            if (_warmedUp) return; // Double-check after acquiring lock
            const string sql = """
                SELECT agent_name, domain, latency_ms, success, user_satisfaction, recorded_at
                FROM agent_performance_metrics
                WHERE recorded_at >= @since
                ORDER BY recorded_at DESC
                """;

            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("since", DateTime.UtcNow.AddDays(-7));

            await using var reader = await cmd.ExecuteReaderAsync();
            var count = 0;
            while (await reader.ReadAsync())
            {
                var metric = new AgentPerformanceMetric
                {
                    Domain = reader.GetString(1),
                    Latency = TimeSpan.FromMilliseconds(reader.GetDouble(2)),
                    Success = reader.GetBoolean(3),
                    UserSatisfaction = reader.IsDBNull(4) ? null : reader.GetDouble(4),
                    RecordedAt = reader.GetDateTime(5)
                };
                var agent = reader.GetString(0);

                // Warm up inner (in-memory) without re-persisting
                await _inner.RecordPerformanceAsync(agent, metric);
                count++;
            }

            _warmedUp = true;
            _logger.LogInformation("SmartRouter warmed up with {Count} metrics from PostgreSQL", count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to warm up SmartRouter from PostgreSQL — starting cold");
            _warmedUp = true; // Don't retry every call
        }
        finally
        {
            _warmupLock.Release();
        }
    }
}
