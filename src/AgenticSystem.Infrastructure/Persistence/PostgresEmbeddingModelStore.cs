using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using Microsoft.Extensions.Logging;
using Npgsql;
using System.Text.Json;

namespace AgenticSystem.Infrastructure.Persistence;

public class PostgresEmbeddingModelStore : IEmbeddingModelStore
{
    private readonly string _connectionString;
    private readonly ILogger<PostgresEmbeddingModelStore> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public PostgresEmbeddingModelStore(string connectionString, ILogger<PostgresEmbeddingModelStore> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public async Task<EmbeddingModelConfig?> GetAsync(string modelId)
    {
        const string sql = "SELECT data FROM embedding_models WHERE id = @id";

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", modelId);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            var json = reader.GetString(0);
            return JsonSerializer.Deserialize<EmbeddingModelConfig>(json, JsonOptions);
        }

        return null;
    }

    public async Task<IEnumerable<EmbeddingModelConfig>> GetAllAsync()
    {
        const string sql = "SELECT data FROM embedding_models ORDER BY created_at DESC";

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(sql, conn);
        var models = new List<EmbeddingModelConfig>();

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var json = reader.GetString(0);
            try
            {
                var model = JsonSerializer.Deserialize<EmbeddingModelConfig>(json, JsonOptions);
                if (model is not null)
                    models.Add(model);
            }
            catch (JsonException)
            {
                // Skip corrupted records
            }
        }

        return models;
    }

    public async Task<EmbeddingModelConfig> GetActiveAsync()
    {
        const string sql = "SELECT data FROM embedding_models WHERE is_active = true LIMIT 1";

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(sql, conn);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            var json = reader.GetString(0);
            var model = JsonSerializer.Deserialize<EmbeddingModelConfig>(json, JsonOptions);
            if (model is not null)
                return model;
        }

        throw new InvalidOperationException("No active embedding model configured.");
    }

    public async Task SaveAsync(EmbeddingModelConfig model)
    {
        await ExecuteWithRetryAsync(async () =>
        {
            const string sql = """
                INSERT INTO embedding_models (id, name, is_active, data, created_at)
                VALUES (@id, @name, @isActive, @data::jsonb, @createdAt)
                ON CONFLICT (id) DO UPDATE SET
                    name = EXCLUDED.name,
                    is_active = EXCLUDED.is_active,
                    data = EXCLUDED.data
                """;

            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("id", model.Id);
            cmd.Parameters.AddWithValue("name", model.Name);
            cmd.Parameters.AddWithValue("isActive", model.IsActive);
            cmd.Parameters.AddWithValue("data", JsonSerializer.Serialize(model, JsonOptions));
            cmd.Parameters.AddWithValue("createdAt", model.CreatedAt);

            await cmd.ExecuteNonQueryAsync();
            _logger.LogDebug("Embedding model saved to PostgreSQL: {ModelId}", model.Id);
        });
    }

    public async Task DeleteAsync(string modelId)
    {
        const string sql = "DELETE FROM embedding_models WHERE id = @id";

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", modelId);

        await cmd.ExecuteNonQueryAsync();
        _logger.LogDebug("Embedding model deleted from PostgreSQL: {ModelId}", modelId);
    }

    public async Task SetActiveAsync(string modelId)
    {
        await ExecuteWithRetryAsync(async () =>
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            await using var tx = await conn.BeginTransactionAsync();

            // Deactivate all models
            await using (var deactivateCmd = new NpgsqlCommand(
                "UPDATE embedding_models SET is_active = false, data = jsonb_set(data, '{isActive}', 'false') WHERE is_active = true", conn, tx))
            {
                await deactivateCmd.ExecuteNonQueryAsync();
            }

            // Activate the selected model
            await using (var activateCmd = new NpgsqlCommand(
                "UPDATE embedding_models SET is_active = true, data = jsonb_set(data, '{isActive}', 'true') WHERE id = @id", conn, tx))
            {
                activateCmd.Parameters.AddWithValue("id", modelId);
                var affected = await activateCmd.ExecuteNonQueryAsync();

                if (affected == 0)
                    throw new InvalidOperationException($"Embedding model '{modelId}' not found.");
            }

            await tx.CommitAsync();
            _logger.LogInformation("Active embedding model set to: {ModelId}", modelId);
        });
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
