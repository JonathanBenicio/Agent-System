using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AgenticSystem.Infrastructure.Persistence.Entities;

/// <summary>A platform-owned tool definition. It has no TenantId by design.</summary>
[Table("platform_agent_tools")]
public sealed class PlatformAgentToolEntity
{
    [Key]
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = "Api";
    public bool RequiresAuth { get; set; }
    public string Type { get; set; } = "Builtin";
    public string MetadataJson { get; set; } = "{}";
    public string Version { get; set; } = "1.0.0";
    public string? VariantName { get; set; }
    public int RolloutPercentage { get; set; } = 100;
    public bool IsDefault { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>A platform-owned skill definition. Tenant activation/customization stays tenant-owned.</summary>
[Table("platform_agent_skills")]
public sealed class PlatformAgentSkillEntity
{
    [Key]
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Domain { get; set; } = "general";
    public string Type { get; set; } = "Instruction";
    public string SystemPromptFragment { get; set; } = string.Empty;
    public string? FewShotExamples { get; set; }
    public bool IsSystem { get; set; } = true;
    public bool IsEnabled { get; set; } = true;
    public string? MetadataJson { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>A platform-owned external provider quota. It is not associated with a synthetic tenant.</summary>
[Table("platform_external_provider_quotas")]
public sealed class PlatformExternalProviderQuotaEntity : IExternalProviderQuotaRecord
{
    [Key]
    public string Id { get; set; } = string.Empty;
    public string ProviderName { get; set; } = string.Empty;
    public string ApiKeyId { get; set; } = string.Empty;
    public long LimitRequests { get; set; }
    public long RemainingRequests { get; set; }
    public long LimitTokens { get; set; }
    public long RemainingTokens { get; set; }
    public DateTime? ResetAt { get; set; }
    public double TotalBalance { get; set; }
    public double BalanceRemaining { get; set; }
    public string Currency { get; set; } = "USD";
    public DateTime LastSyncAt { get; set; } = DateTime.UtcNow;
}

/// <summary>A platform event awaiting dispatch. Platform events do not carry TenantId.</summary>
[Table("platform_outbox_messages")]
public sealed class PlatformOutboxMessageEntity
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    public string EventType { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }
    public string? Error { get; set; }
}

/// <summary>Shared quota fields used by tenant and platform quota records.</summary>
public interface IExternalProviderQuotaRecord
{
    string ProviderName { get; set; }
    string ApiKeyId { get; set; }
    long LimitRequests { get; set; }
    long RemainingRequests { get; set; }
    long LimitTokens { get; set; }
    long RemainingTokens { get; set; }
    DateTime? ResetAt { get; set; }
    double TotalBalance { get; set; }
    double BalanceRemaining { get; set; }
    string Currency { get; set; }
    DateTime LastSyncAt { get; set; }
}
