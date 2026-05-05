using System.Collections.Concurrent;
using System.Security.Cryptography;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Infrastructure.RAG;

public sealed class InMemoryRerankingAssetStore : IRerankingAssetStore
{
    private readonly ConcurrentDictionary<string, StoredRerankingAsset> _assets = new(StringComparer.OrdinalIgnoreCase);

    public Task<StoredRerankingAsset?> GetAsync(string tenantId, string assetType, CancellationToken ct = default)
    {
        _assets.TryGetValue(BuildKey(tenantId, assetType), out var asset);
        return Task.FromResult(asset);
    }

    public Task<StoredRerankingAsset> SaveAsync(RerankingAssetUpload upload, CancellationToken ct = default)
    {
        var tenantId = string.IsNullOrWhiteSpace(upload.TenantId) ? Tenant.DefaultTenantId : upload.TenantId;
        var asset = new StoredRerankingAsset
        {
            TenantId = tenantId,
            AssetType = upload.AssetType,
            FileName = upload.FileName,
            ContentType = upload.ContentType,
            Content = upload.Content,
            ContentHash = Convert.ToHexString(SHA256.HashData(upload.Content)),
            UpdatedAt = DateTime.UtcNow
        };

        _assets[BuildKey(tenantId, upload.AssetType)] = asset;
        return Task.FromResult(asset);
    }

    private static string BuildKey(string tenantId, string assetType)
        => $"{tenantId}:{assetType}";
}