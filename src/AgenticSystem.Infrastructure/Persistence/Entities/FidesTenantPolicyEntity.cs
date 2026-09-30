using AgenticSystem.Core.Interfaces;

namespace AgenticSystem.Infrastructure.Persistence.Entities;

public sealed class FidesTenantPolicyEntity : ITenantEntity
{
    public string TenantId { get; set; } = string.Empty;
    public string EnabledDetectorsJson { get; set; } = "{}";
    public long Version { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
