namespace AgenticSystem.Infrastructure.RAG;

public interface IRerankingAssetStore
{
    Task<StoredRerankingAsset?> GetAsync(string tenantId, string assetType, CancellationToken ct = default);
    Task<StoredRerankingAsset> SaveAsync(RerankingAssetUpload upload, CancellationToken ct = default);
}

public sealed class StoredRerankingAsset
{
    public string TenantId { get; init; } = string.Empty;
    public string AssetType { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = "application/octet-stream";
    public byte[] Content { get; init; } = Array.Empty<byte>();
    public string ContentHash { get; init; } = string.Empty;
    public DateTime UpdatedAt { get; init; } = DateTime.UtcNow;
}

public sealed class RerankingAssetUpload
{
    public string TenantId { get; init; } = string.Empty;
    public string AssetType { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = "application/octet-stream";
    public byte[] Content { get; init; } = Array.Empty<byte>();
}

public static class RerankingAssetTypes
{
    public const string Model = "model";
    public const string Vocabulary = "vocabulary";
}