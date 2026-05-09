using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Infrastructure.Persistence.Entities;

[Table("AgentPolicies")]
public class AgentPolicyEntity
{
    [Key]
    [MaxLength(50)]
    public string Id { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [MaxLength(100)]
    public string? AgentNamePattern { get; set; }

    [MaxLength(50)]
    public string? TenantId { get; set; }

    public AutonomyLevel MaxAutonomyLevel { get; set; }

    public List<string> AllowedToolCategories { get; set; } = new();

    public List<string> DeniedTools { get; set; } = new();

    public List<string> AllowedProviders { get; set; } = new();

    [Column(TypeName = "decimal(18,4)")]
    public decimal? MaxCostPerRequest { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal? MaxCostPerDay { get; set; }

    public int? MaxTokensPerRequest { get; set; }

    public bool RequireFinalApproval { get; set; }

    public ToolRiskLevel ApprovalThreshold { get; set; }

    public List<string> ContentFilters { get; set; } = new();

    public int Priority { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public AgentPolicy ToModel()
    {
        return new AgentPolicy
        {
            Id = Id,
            Name = Name,
            Description = Description,
            AgentNamePattern = AgentNamePattern,
            TenantId = TenantId,
            MaxAutonomyLevel = MaxAutonomyLevel,
            AllowedToolCategories = AllowedToolCategories,
            DeniedTools = DeniedTools,
            AllowedProviders = AllowedProviders,
            MaxCostPerRequest = MaxCostPerRequest,
            MaxCostPerDay = MaxCostPerDay,
            MaxTokensPerRequest = MaxTokensPerRequest,
            RequireFinalApproval = RequireFinalApproval,
            ApprovalThreshold = ApprovalThreshold,
            ContentFilters = ContentFilters,
            Priority = Priority,
            IsActive = IsActive,
            CreatedAt = CreatedAt,
            UpdatedAt = UpdatedAt
        };
    }

    public static AgentPolicyEntity FromModel(AgentPolicy model)
    {
        return new AgentPolicyEntity
        {
            Id = model.Id,
            Name = model.Name,
            Description = model.Description,
            AgentNamePattern = model.AgentNamePattern,
            TenantId = model.TenantId,
            MaxAutonomyLevel = model.MaxAutonomyLevel,
            AllowedToolCategories = model.AllowedToolCategories,
            DeniedTools = model.DeniedTools,
            AllowedProviders = model.AllowedProviders,
            MaxCostPerRequest = model.MaxCostPerRequest,
            MaxCostPerDay = model.MaxCostPerDay,
            MaxTokensPerRequest = model.MaxTokensPerRequest,
            RequireFinalApproval = model.RequireFinalApproval,
            ApprovalThreshold = model.ApprovalThreshold,
            ContentFilters = model.ContentFilters,
            Priority = model.Priority,
            IsActive = model.IsActive,
            CreatedAt = model.CreatedAt,
            UpdatedAt = model.UpdatedAt
        };
    }
}
