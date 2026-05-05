using System.Security.Cryptography;
using AgenticSystem.Infrastructure.RAG;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace AgenticSystem.Infrastructure.Persistence;

public sealed class PostgresRerankingAssetStore : IRerankingAssetStore
{
    private readonly string _connectionString;
    private readonly ILogger<PostgresRerankingAssetStore> _logger;
    private readonly SemaphoreSlim _schemaLock = new(1, 1);
    private bool _schemaReady;

    public PostgresRerankingAssetStore(string connectionString, ILogger<PostgresRerankingAssetStore> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public async Task<StoredRerankingAsset?> GetAsync(string tenantId, string assetType, CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        const string sql = """
            SELECT tenant_id, asset_type, file_name, content_type, content, content_hash, updated_at
            FROM reranking_assets
            WHERE tenant_id = @tenantId AND asset_type = @assetType
            """;

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tenantId", tenantId);
        cmd.Parameters.AddWithValue("assetType", assetType);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            return null;
        }

        return new StoredRerankingAsset
        {
            TenantId = reader.GetString(0),
            AssetType = reader.GetString(1),
            FileName = reader.GetString(2),
            ContentType = reader.GetString(3),
            Content = (byte[])reader[4],
            ContentHash = reader.GetString(5),
            UpdatedAt = reader.GetDateTime(6)
        };
    }

    public async Task<StoredRerankingAsset> SaveAsync(RerankingAssetUpload upload, CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);

        var contentHash = Convert.ToHexString(SHA256.HashData(upload.Content));
        const string sql = """
            INSERT INTO reranking_assets (tenant_id, asset_type, file_name, content_type, content, content_hash, updated_at)
            VALUES (@tenantId, @assetType, @fileName, @contentType, @content, @contentHash, @updatedAt)
            ON CONFLICT (tenant_id, asset_type) DO UPDATE SET
                file_name = EXCLUDED.file_name,
                content_type = EXCLUDED.content_type,
                content = EXCLUDED.content,
                content_hash = EXCLUDED.content_hash,
                updated_at = EXCLUDED.updated_at
            """;

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tenantId", upload.TenantId);
        cmd.Parameters.AddWithValue("assetType", upload.AssetType);
        cmd.Parameters.AddWithValue("fileName", upload.FileName);
        cmd.Parameters.AddWithValue("contentType", upload.ContentType);
        cmd.Parameters.AddWithValue("content", upload.Content);
        cmd.Parameters.AddWithValue("contentHash", contentHash);
        cmd.Parameters.AddWithValue("updatedAt", DateTime.UtcNow);
        await cmd.ExecuteNonQueryAsync(ct);

        _logger.LogInformation("Rerank asset {AssetType} persisted for tenant {TenantId}", upload.AssetType, upload.TenantId);
        return new StoredRerankingAsset
        {
            TenantId = upload.TenantId,
            AssetType = upload.AssetType,
            FileName = upload.FileName,
            ContentType = upload.ContentType,
            Content = upload.Content,
            ContentHash = contentHash,
            UpdatedAt = DateTime.UtcNow
        };
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
                CREATE TABLE IF NOT EXISTS reranking_assets (
                    tenant_id text NOT NULL,
                    asset_type text NOT NULL,
                    file_name text NOT NULL,
                    content_type text NOT NULL,
                    content bytea NOT NULL,
                    content_hash text NOT NULL,
                    updated_at timestamp with time zone NOT NULL,
                    PRIMARY KEY (tenant_id, asset_type)
                );
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