using System.Security.Cryptography;
using AgenticSystem.Infrastructure.Persistence.Entities;
using AgenticSystem.Infrastructure.RAG;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Infrastructure.Persistence;

public sealed class PostgresRerankingAssetStore : IRerankingAssetStore
{
    private readonly IDbContextFactory<AgenticDbContext> _dbContextFactory;
    private readonly ILogger<PostgresRerankingAssetStore> _logger;

    public PostgresRerankingAssetStore(IDbContextFactory<AgenticDbContext> dbContextFactory, ILogger<PostgresRerankingAssetStore> logger)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    public async Task<StoredRerankingAsset?> GetAsync(string tenantId, string assetType, CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var entity = await db.RerankingAssets
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.AssetType == assetType, ct);

        if (entity is null)
        {
            return null;
        }

        return new StoredRerankingAsset
        {
            TenantId = entity.TenantId,
            AssetType = entity.AssetType,
            FileName = entity.FileName,
            ContentType = entity.ContentType,
            Content = entity.Content,
            ContentHash = entity.ContentHash,
            UpdatedAt = entity.UpdatedAt
        };
    }

    public async Task<StoredRerankingAsset> SaveAsync(RerankingAssetUpload upload, CancellationToken ct = default)
    {
        var contentHash = Convert.ToHexString(SHA256.HashData(upload.Content));
        var updatedAt = DateTime.UtcNow;

        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var entity = await db.RerankingAssets.FirstOrDefaultAsync(
            item => item.TenantId == upload.TenantId && item.AssetType == upload.AssetType,
            ct);

        if (entity is null)
        {
            db.RerankingAssets.Add(new RerankingAssetEntity
            {
                TenantId = upload.TenantId,
                AssetType = upload.AssetType,
                FileName = upload.FileName,
                ContentType = upload.ContentType,
                Content = upload.Content,
                ContentHash = contentHash,
                UpdatedAt = updatedAt
            });
        }
        else
        {
            entity.FileName = upload.FileName;
            entity.ContentType = upload.ContentType;
            entity.Content = upload.Content;
            entity.ContentHash = contentHash;
            entity.UpdatedAt = updatedAt;
        }

        await db.SaveChangesAsync(ct);

        _logger.LogInformation("Rerank asset {AssetType} persisted for tenant {TenantId} via EF Core", upload.AssetType, upload.TenantId);
        return new StoredRerankingAsset
        {
            TenantId = upload.TenantId,
            AssetType = upload.AssetType,
            FileName = upload.FileName,
            ContentType = upload.ContentType,
            Content = upload.Content,
            ContentHash = contentHash,
            UpdatedAt = updatedAt
        };
    }
}
