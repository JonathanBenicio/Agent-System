using System.Text.Json;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace AgenticSystem.Infrastructure.Persistence;

public sealed class PostgresScheduledTaskStore : IScheduledTaskStore
{
    private readonly string _connectionString;
    private readonly ILogger<PostgresScheduledTaskStore> _logger;
    private readonly SemaphoreSlim _schemaLock = new(1, 1);
    private bool _schemaReady;

    public PostgresScheduledTaskStore(string connectionString, ILogger<PostgresScheduledTaskStore> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public async Task<ScheduledTask> SaveTaskAsync(ScheduledTask task, CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);

        const string sql = """
            INSERT INTO scheduled_tasks (id, name, status, next_run_at, payload, updated_at)
            VALUES (@id, @name, @status, @nextRunAt, @payload::jsonb, @updatedAt)
            ON CONFLICT (id) DO UPDATE SET
                name = EXCLUDED.name,
                status = EXCLUDED.status,
                next_run_at = EXCLUDED.next_run_at,
                payload = EXCLUDED.payload,
                updated_at = EXCLUDED.updated_at
            """;

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", task.Id);
        cmd.Parameters.AddWithValue("name", task.Name);
        cmd.Parameters.AddWithValue("status", task.Status.ToString());
        cmd.Parameters.AddWithValue("nextRunAt", (object?)task.NextRunAt ?? DBNull.Value);
        cmd.Parameters.AddWithValue("payload", JsonSerializer.Serialize(task));
        cmd.Parameters.AddWithValue("updatedAt", DateTime.UtcNow);
        await cmd.ExecuteNonQueryAsync(ct);
        return task;
    }

    public async Task<ScheduledTask?> GetTaskAsync(string taskId, CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        const string sql = "SELECT payload FROM scheduled_tasks WHERE id = @id";

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", taskId);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is string json ? JsonSerializer.Deserialize<ScheduledTask>(json) : null;
    }

    public async Task<IReadOnlyList<ScheduledTask>> GetAllTasksAsync(CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        const string sql = "SELECT payload FROM scheduled_tasks ORDER BY updated_at DESC";

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        var items = new List<ScheduledTask>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var task = JsonSerializer.Deserialize<ScheduledTask>(reader.GetString(0));
            if (task is not null)
            {
                items.Add(task);
            }
        }

        return items;
    }

    public async Task DeleteTaskAsync(string taskId, CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        const string sql = "DELETE FROM scheduled_tasks WHERE id = @id";

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", taskId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<TriggerRule> SaveRuleAsync(TriggerRule rule, CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);

        const string sql = """
            INSERT INTO trigger_rules (id, name, enabled, payload, updated_at)
            VALUES (@id, @name, @enabled, @payload::jsonb, @updatedAt)
            ON CONFLICT (id) DO UPDATE SET
                name = EXCLUDED.name,
                enabled = EXCLUDED.enabled,
                payload = EXCLUDED.payload,
                updated_at = EXCLUDED.updated_at
            """;

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", rule.Id);
        cmd.Parameters.AddWithValue("name", rule.Name);
        cmd.Parameters.AddWithValue("enabled", rule.Enabled);
        cmd.Parameters.AddWithValue("payload", JsonSerializer.Serialize(rule));
        cmd.Parameters.AddWithValue("updatedAt", DateTime.UtcNow);
        await cmd.ExecuteNonQueryAsync(ct);
        return rule;
    }

    public async Task<TriggerRule?> GetRuleAsync(string ruleId, CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        const string sql = "SELECT payload FROM trigger_rules WHERE id = @id";

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", ruleId);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is string json ? JsonSerializer.Deserialize<TriggerRule>(json) : null;
    }

    public async Task<IReadOnlyList<TriggerRule>> GetAllRulesAsync(CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        const string sql = "SELECT payload FROM trigger_rules ORDER BY updated_at DESC";

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        var items = new List<TriggerRule>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var rule = JsonSerializer.Deserialize<TriggerRule>(reader.GetString(0));
            if (rule is not null)
            {
                items.Add(rule);
            }
        }

        return items;
    }

    public async Task DeleteRuleAsync(string ruleId, CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        const string sql = "DELETE FROM trigger_rules WHERE id = @id";

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", ruleId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<TaskExecution> SaveExecutionAsync(TaskExecution execution, CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);

        const string sql = """
            INSERT INTO scheduled_task_executions (execution_id, task_id, started_at, completed_at, success, payload)
            VALUES (@executionId, @taskId, @startedAt, @completedAt, @success, @payload::jsonb)
            ON CONFLICT (execution_id) DO UPDATE SET
                task_id = EXCLUDED.task_id,
                started_at = EXCLUDED.started_at,
                completed_at = EXCLUDED.completed_at,
                success = EXCLUDED.success,
                payload = EXCLUDED.payload
            """;

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("executionId", execution.ExecutionId);
        cmd.Parameters.AddWithValue("taskId", execution.TaskId);
        cmd.Parameters.AddWithValue("startedAt", execution.StartedAt);
        cmd.Parameters.AddWithValue("completedAt", (object?)execution.CompletedAt ?? DBNull.Value);
        cmd.Parameters.AddWithValue("success", execution.Success);
        cmd.Parameters.AddWithValue("payload", JsonSerializer.Serialize(execution));
        await cmd.ExecuteNonQueryAsync(ct);
        return execution;
    }

    private async Task EnsureSchemaAsync(CancellationToken ct)
    {
        if (_schemaReady)
        {
            return;
        }

        await _schemaLock.WaitAsync(ct);
        try
        {
            if (_schemaReady)
            {
                return;
            }

            const string sql = """
                CREATE TABLE IF NOT EXISTS scheduled_tasks (
                    id text PRIMARY KEY,
                    name text NOT NULL,
                    status text NOT NULL,
                    next_run_at timestamp with time zone NULL,
                    payload jsonb NOT NULL,
                    updated_at timestamp with time zone NOT NULL
                );

                CREATE TABLE IF NOT EXISTS trigger_rules (
                    id text PRIMARY KEY,
                    name text NOT NULL,
                    enabled boolean NOT NULL,
                    payload jsonb NOT NULL,
                    updated_at timestamp with time zone NOT NULL
                );

                CREATE TABLE IF NOT EXISTS scheduled_task_executions (
                    execution_id text PRIMARY KEY,
                    task_id text NOT NULL,
                    started_at timestamp with time zone NOT NULL,
                    completed_at timestamp with time zone NULL,
                    success boolean NOT NULL,
                    payload jsonb NOT NULL
                );

                CREATE INDEX IF NOT EXISTS ix_scheduled_tasks_status ON scheduled_tasks(status);
                CREATE INDEX IF NOT EXISTS ix_trigger_rules_enabled ON trigger_rules(enabled);
                CREATE INDEX IF NOT EXISTS ix_scheduled_task_executions_task_id ON scheduled_task_executions(task_id);
                """;

            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(ct);
            await using var cmd = new NpgsqlCommand(sql, conn);
            await cmd.ExecuteNonQueryAsync(ct);
            _schemaReady = true;
        }
        finally
        {
            _schemaLock.Release();
        }
    }
}