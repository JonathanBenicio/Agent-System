using System.Text.Json;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace AgenticSystem.Infrastructure.Persistence;

public sealed class PostgresConfigStore : IConfigStore
{
    private readonly string _connectionString;
    private readonly ILogger<PostgresConfigStore> _logger;
    private readonly SemaphoreSlim _schemaLock = new(1, 1);
    private bool _schemaReady;

    public PostgresConfigStore(string connectionString, ILogger<PostgresConfigStore> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public async Task<ConfigEntry?> GetByKeyAsync(string key)
    {
        await EnsureSchemaAsync(CancellationToken.None);

        const string sql = """
            SELECT id, key, value, encrypted_value, is_secret, category, status, description, provider, created_at, updated_at, expires_at, metadata
            FROM config_entries
            WHERE key = @key
            """;

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("key", key);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

        return MapEntry(reader);
    }

    public async Task<IEnumerable<ConfigEntry>> GetAllAsync(ConfigCategory? category = null)
    {
        await EnsureSchemaAsync(CancellationToken.None);

        const string sql = """
            SELECT id, key, value, encrypted_value, is_secret, category, status, description, provider, created_at, updated_at, expires_at, metadata
            FROM config_entries
            WHERE (@category IS NULL OR category = @category)
            ORDER BY key
            """;

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("category", (object?)category?.ToString() ?? DBNull.Value);

        var entries = new List<ConfigEntry>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            entries.Add(MapEntry(reader));
        }

        return entries;
    }

    public async Task SaveAsync(ConfigEntry entry)
    {
        await EnsureSchemaAsync(CancellationToken.None);

        const string sql = """
            INSERT INTO config_entries (id, key, value, encrypted_value, is_secret, category, status, description, provider, created_at, updated_at, expires_at, metadata)
            VALUES (@id, @key, @value, @encryptedValue, @isSecret, @category, @status, @description, @provider, @createdAt, @updatedAt, @expiresAt, @metadata::jsonb)
            ON CONFLICT (key) DO UPDATE SET
                value = EXCLUDED.value,
                encrypted_value = EXCLUDED.encrypted_value,
                is_secret = EXCLUDED.is_secret,
                category = EXCLUDED.category,
                status = EXCLUDED.status,
                description = EXCLUDED.description,
                provider = EXCLUDED.provider,
                updated_at = EXCLUDED.updated_at,
                expires_at = EXCLUDED.expires_at,
                metadata = EXCLUDED.metadata
            """;

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", entry.Id);
        cmd.Parameters.AddWithValue("key", entry.Key);
        cmd.Parameters.AddWithValue("value", entry.Value);
        cmd.Parameters.AddWithValue("encryptedValue", (object?)entry.EncryptedValue ?? DBNull.Value);
        cmd.Parameters.AddWithValue("isSecret", entry.IsSecret);
        cmd.Parameters.AddWithValue("category", entry.Category.ToString());
        cmd.Parameters.AddWithValue("status", entry.Status.ToString());
        cmd.Parameters.AddWithValue("description", (object?)entry.Description ?? DBNull.Value);
        cmd.Parameters.AddWithValue("provider", (object?)entry.Provider ?? DBNull.Value);
        cmd.Parameters.AddWithValue("createdAt", entry.CreatedAt);
        cmd.Parameters.AddWithValue("updatedAt", entry.UpdatedAt);
        cmd.Parameters.AddWithValue("expiresAt", (object?)entry.ExpiresAt ?? DBNull.Value);
        cmd.Parameters.AddWithValue("metadata", JsonSerializer.Serialize(entry.Metadata));
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteAsync(string key)
    {
        await EnsureSchemaAsync(CancellationToken.None);

        const string sql = "DELETE FROM config_entries WHERE key = @key";
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("key", key);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<IEnumerable<ConfigChangeLog>> GetChangeLogsAsync(string? key = null, int limit = 50)
    {
        await EnsureSchemaAsync(CancellationToken.None);

        const string sql = """
            SELECT id, config_key, action, changed_by, changed_at, previous_value_hash, new_value_hash
            FROM config_change_logs
            WHERE (@key IS NULL OR config_key = @key)
            ORDER BY changed_at DESC
            LIMIT @limit
            """;

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("key", (object?)key ?? DBNull.Value);
        cmd.Parameters.AddWithValue("limit", limit);

        var logs = new List<ConfigChangeLog>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            logs.Add(new ConfigChangeLog
            {
                Id = reader.GetString(0),
                ConfigKey = reader.GetString(1),
                Action = reader.GetString(2),
                ChangedBy = reader.IsDBNull(3) ? null : reader.GetString(3),
                ChangedAt = reader.GetDateTime(4),
                PreviousValueHash = reader.IsDBNull(5) ? null : reader.GetString(5),
                NewValueHash = reader.IsDBNull(6) ? null : reader.GetString(6)
            });
        }

        return logs;
    }

    public async Task SaveChangeLogAsync(ConfigChangeLog log)
    {
        await EnsureSchemaAsync(CancellationToken.None);

        const string sql = """
            INSERT INTO config_change_logs (id, config_key, action, changed_by, changed_at, previous_value_hash, new_value_hash)
            VALUES (@id, @configKey, @action, @changedBy, @changedAt, @previousValueHash, @newValueHash)
            """;

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", log.Id);
        cmd.Parameters.AddWithValue("configKey", log.ConfigKey);
        cmd.Parameters.AddWithValue("action", log.Action);
        cmd.Parameters.AddWithValue("changedBy", (object?)log.ChangedBy ?? DBNull.Value);
        cmd.Parameters.AddWithValue("changedAt", log.ChangedAt);
        cmd.Parameters.AddWithValue("previousValueHash", (object?)log.PreviousValueHash ?? DBNull.Value);
        cmd.Parameters.AddWithValue("newValueHash", (object?)log.NewValueHash ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync();
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
                CREATE TABLE IF NOT EXISTS config_entries (
                    id text PRIMARY KEY,
                    key text NOT NULL UNIQUE,
                    value text NOT NULL,
                    encrypted_value text NULL,
                    is_secret boolean NOT NULL,
                    category text NOT NULL,
                    status text NOT NULL,
                    description text NULL,
                    provider text NULL,
                    created_at timestamp with time zone NOT NULL,
                    updated_at timestamp with time zone NOT NULL,
                    expires_at timestamp with time zone NULL,
                    metadata jsonb NOT NULL DEFAULT '{}'::jsonb
                );

                CREATE TABLE IF NOT EXISTS config_change_logs (
                    id text PRIMARY KEY,
                    config_key text NOT NULL,
                    action text NOT NULL,
                    changed_by text NULL,
                    changed_at timestamp with time zone NOT NULL,
                    previous_value_hash text NULL,
                    new_value_hash text NULL
                );

                CREATE INDEX IF NOT EXISTS ix_config_entries_category ON config_entries(category);
                CREATE INDEX IF NOT EXISTS ix_config_change_logs_key_changed_at ON config_change_logs(config_key, changed_at DESC);
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

    private static ConfigEntry MapEntry(NpgsqlDataReader reader)
    {
        return new ConfigEntry
        {
            Id = reader.GetString(0),
            Key = reader.GetString(1),
            Value = reader.GetString(2),
            EncryptedValue = reader.IsDBNull(3) ? null : reader.GetString(3),
            IsSecret = reader.GetBoolean(4),
            Category = Enum.TryParse<ConfigCategory>(reader.GetString(5), out var category) ? category : ConfigCategory.General,
            Status = Enum.TryParse<ConfigEntryStatus>(reader.GetString(6), out var status) ? status : ConfigEntryStatus.Active,
            Description = reader.IsDBNull(7) ? null : reader.GetString(7),
            Provider = reader.IsDBNull(8) ? null : reader.GetString(8),
            CreatedAt = reader.GetDateTime(9),
            UpdatedAt = reader.GetDateTime(10),
            ExpiresAt = reader.IsDBNull(11) ? null : reader.GetDateTime(11),
            Metadata = JsonSerializer.Deserialize<Dictionary<string, string>>(reader.GetString(12)) ?? new Dictionary<string, string>()
        };
    }
}