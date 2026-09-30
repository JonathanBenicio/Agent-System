namespace AgenticSystem.Core.Models;

public sealed class FidesTenantPolicy
{
    public string TenantId { get; set; } = string.Empty;
    public Dictionary<string, bool> EnabledDetectors { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public long Version { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public enum FidesMediaScanStatus
{
    NoSensitiveContent,
    SensitiveContentRedacted,
    Uncertain
}

public sealed class FidesMediaScanResult
{
    public FidesMediaScanStatus Status { get; init; }
    public byte[]? SanitizedContent { get; init; }
    public string? SanitizedMediaType { get; init; }
    public byte[]? RedactedContent { get; init; }
    public string? RedactedMediaType { get; init; }
    public IReadOnlyList<string> DetectedCategories { get; init; } = [];
}
