using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using Microsoft.Extensions.Logging;
using Npgsql;
using System.Text.Json;

namespace AgenticSystem.Infrastructure.Persistence;

/// <summary>
/// Implementação PostgreSQL de ISessionStore para produção.
/// Requer tabela 'sessions' criada via migration (Flyway/Liquibase).
/// </summary>
public class PostgresSessionStore : ISessionStore
{
    private readonly string _connectionString;
    private readonly ILogger<PostgresSessionStore> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public PostgresSessionStore(string connectionString, ILogger<PostgresSessionStore> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public async Task SaveAsync(SessionData session, CancellationToken ct = default)
    {
        await ExecuteWithRetryAsync(async () =>
        {
            const string sql = """
                INSERT INTO sessions (id, user_id, tenant_id, data, started_at, ended_at, is_consolidated)
                VALUES (@id, @userId, @tenantId, @data::jsonb, @startedAt, @endedAt, @isConsolidated)
                ON CONFLICT (id) DO UPDATE SET
                    data = EXCLUDED.data,
                    ended_at = EXCLUDED.ended_at,
                    is_consolidated = EXCLUDED.is_consolidated
                """;

            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(ct);

            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("id", session.Id);
            cmd.Parameters.AddWithValue("userId", session.UserId);
            cmd.Parameters.AddWithValue("tenantId", session.TenantId);
            cmd.Parameters.AddWithValue("data", JsonSerializer.Serialize(session, JsonOptions));
            cmd.Parameters.AddWithValue("startedAt", session.StartedAt);
            cmd.Parameters.AddWithValue("endedAt", (object?)session.EndedAt ?? DBNull.Value);
            cmd.Parameters.AddWithValue("isConsolidated", session.IsConsolidated);

            await cmd.ExecuteNonQueryAsync(ct);
            _logger.LogDebug("Session saved to PostgreSQL: {SessionId}", session.Id);
        }, ct);
    }

    public async Task<SessionData?> GetAsync(string sessionId, CancellationToken ct = default)
    {
        const string sql = "SELECT data FROM sessions WHERE id = @id";

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", sessionId);

        var result = await cmd.ExecuteScalarAsync(ct);
        if (result is null or DBNull)
            return null;

        try
        {
            return JsonSerializer.Deserialize<SessionData>((string)result, JsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize session {SessionId}", sessionId);
            return null;
        }
    }

    public async Task<IReadOnlyList<SessionData>> GetByUserAsync(string userId, int maxResults = 10, CancellationToken ct = default)
    {
        const string sql = """
            SELECT data FROM sessions
            WHERE user_id = @userId
            ORDER BY started_at DESC
            LIMIT @limit
            """;

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("userId", userId);
        cmd.Parameters.AddWithValue("limit", maxResults);

        return await ReadSessionsAsync(cmd, ct);
    }

    public async Task<IReadOnlyList<SessionData>> GetByTenantAsync(string tenantId, string? userId = null, int maxResults = 10, CancellationToken ct = default)
    {
        var sql = """
            SELECT data FROM sessions
            WHERE tenant_id = @tenantId
            """;

        if (userId is not null)
            sql += " AND user_id = @userId";

        sql += " ORDER BY started_at DESC LIMIT @limit";

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tenantId", tenantId);
        cmd.Parameters.AddWithValue("limit", maxResults);
        if (userId is not null)
            cmd.Parameters.AddWithValue("userId", userId);

        return await ReadSessionsAsync(cmd, ct);
    }

    public async Task DeleteAsync(string sessionId, CancellationToken ct = default)
    {
        const string sql = "DELETE FROM sessions WHERE id = @id";

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", sessionId);

        await cmd.ExecuteNonQueryAsync(ct);
        _logger.LogDebug("Session deleted from PostgreSQL: {SessionId}", sessionId);
    }

    public async Task<bool> ExistsAsync(string sessionId, CancellationToken ct = default)
    {
        const string sql = "SELECT EXISTS(SELECT 1 FROM sessions WHERE id = @id)";

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", sessionId);

        var result = await cmd.ExecuteScalarAsync(ct);
        return result is true;
    }

    private static async Task<IReadOnlyList<SessionData>> ReadSessionsAsync(NpgsqlCommand cmd, CancellationToken ct)
    {
        var sessions = new List<SessionData>();

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var json = reader.GetString(0);
            try
            {
                var session = JsonSerializer.Deserialize<SessionData>(json, JsonOptions);
                if (session is not null)
                    sessions.Add(session);
            }
            catch (JsonException)
            {
                // Skip corrupted session records rather than failing entire query
            }
        }

        return sessions;
    }

    private static readonly Random s_jitter = new();

    private async Task ExecuteWithRetryAsync(Func<Task> action, CancellationToken ct, int maxRetries = 3)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                await action();
                return;
            }
            catch (NpgsqlException ex) when (ex.IsTransient && attempt < maxRetries - 1)
            {
                var baseDelay = 100 * Math.Pow(2, attempt);
                var jitter = s_jitter.Next(0, (int)(baseDelay * 0.5));
                var delay = TimeSpan.FromMilliseconds(baseDelay + jitter);
                _logger.LogWarning(ex, "Transient PostgreSQL error (attempt {Attempt}/{MaxRetries}). Retrying in {Delay}ms.",
                    attempt + 1, maxRetries, delay.TotalMilliseconds);
                await Task.Delay(delay, ct);
            }
        }
    }
}
