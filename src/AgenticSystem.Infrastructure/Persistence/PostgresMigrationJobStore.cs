using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using Microsoft.Extensions.Logging;
using Npgsql;
using System.Text.Json;

namespace AgenticSystem.Infrastructure.Persistence;

public class PostgresMigrationJobStore : IMigrationJobStore
{
    private readonly string _connectionString;
    private readonly ILogger<PostgresMigrationJobStore> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public PostgresMigrationJobStore(string connectionString, ILogger<PostgresMigrationJobStore> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public async Task<EmbeddingMigrationJob?> GetAsync(string jobId)
    {
        const string sql = "SELECT data FROM migration_jobs WHERE id = @id";

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", jobId);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            var json = reader.GetString(0);
            return JsonSerializer.Deserialize<EmbeddingMigrationJob>(json, JsonOptions);
        }

        return null;
    }

    public async Task<IEnumerable<EmbeddingMigrationJob>> GetAllAsync()
    {
        const string sql = "SELECT data FROM migration_jobs ORDER BY created_at DESC";

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(sql, conn);
        var jobs = new List<EmbeddingMigrationJob>();

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var json = reader.GetString(0);
            try
            {
                var job = JsonSerializer.Deserialize<EmbeddingMigrationJob>(json, JsonOptions);
                if (job is not null)
                    jobs.Add(job);
            }
            catch (JsonException)
            {
                // Skip corrupted records
            }
        }

        return jobs;
    }

    public async Task SaveAsync(EmbeddingMigrationJob job)
    {
        await ExecuteWithRetryAsync(async () =>
        {
            const string sql = """
                INSERT INTO migration_jobs (id, status, data, created_at)
                VALUES (@id, @status, @data::jsonb, @createdAt)
                ON CONFLICT (id) DO UPDATE SET
                    status = EXCLUDED.status,
                    data = EXCLUDED.data
                """;

            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("id", job.Id);
            cmd.Parameters.AddWithValue("status", job.Status.ToString());
            cmd.Parameters.AddWithValue("data", JsonSerializer.Serialize(job, JsonOptions));
            cmd.Parameters.AddWithValue("createdAt", job.CreatedAt);

            await cmd.ExecuteNonQueryAsync();
            _logger.LogDebug("Migration job saved to PostgreSQL: {JobId}", job.Id);
        });
    }

    public async Task DeleteAsync(string jobId)
    {
        const string sql = "DELETE FROM migration_jobs WHERE id = @id";

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", jobId);

        await cmd.ExecuteNonQueryAsync();
        _logger.LogDebug("Migration job deleted from PostgreSQL: {JobId}", jobId);
    }

    private static readonly Random s_jitter = new();

    private async Task ExecuteWithRetryAsync(Func<Task> action, int maxRetries = 3)
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
                await Task.Delay(delay);
            }
        }
    }
}
