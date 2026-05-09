using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Infrastructure.Persistence.Entities;

[Table("agent_versions")]
public class AgentVersionEntity
{
    [Key]
    [MaxLength(64)]
    public string Id { get; set; } = string.Empty;

    [Required]
    [MaxLength(256)]
    public string AgentName { get; set; } = string.Empty;

    public int VersionNumber { get; set; }

    [MaxLength(128)]
    public string Label { get; set; } = string.Empty;

    public AgentVersionStatus Status { get; set; }

    public AgentVersionEnvironment Environment { get; set; }

    [Required]
    public string SystemPrompt { get; set; } = string.Empty;

    [MaxLength(128)]
    public string? ModelProvider { get; set; }

    [MaxLength(128)]
    public string? ModelId { get; set; }

    [Required]
    public List<string> Tools { get; set; } = new();

    public string? PolicySnapshotJson { get; set; }

    [Required]
    public string ParametersJson { get; set; } = "{}";

    public string? Description { get; set; }

    public string? ChangeLog { get; set; }

    [MaxLength(128)]
    public string? CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? PromotedAt { get; set; }

    [MaxLength(128)]
    public string? PromotedBy { get; set; }

    [MaxLength(128)]
    public string? ConfigHash { get; set; }

    [MaxLength(64)]
    public string? ParentVersionId { get; set; }

    public AgentVersion ToModel()
    {
        return new AgentVersion
        {
            Id = Id,
            AgentName = AgentName,
            VersionNumber = VersionNumber,
            Label = Label,
            Status = Status,
            Environment = Environment,
            SystemPrompt = SystemPrompt,
            ModelProvider = ModelProvider,
            ModelId = ModelId,
            Tools = Tools,
            PolicySnapshotJson = PolicySnapshotJson,
            Parameters = string.IsNullOrWhiteSpace(ParametersJson)
                ? new Dictionary<string, object>()
                : System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(ParametersJson) ?? new(),
            Description = Description,
            ChangeLog = ChangeLog,
            CreatedBy = CreatedBy,
            CreatedAt = CreatedAt,
            PromotedAt = PromotedAt,
            PromotedBy = PromotedBy,
            ConfigHash = ConfigHash,
            ParentVersionId = ParentVersionId
        };
    }

    public static AgentVersionEntity FromModel(AgentVersion model)
    {
        return new AgentVersionEntity
        {
            Id = model.Id,
            AgentName = model.AgentName,
            VersionNumber = model.VersionNumber,
            Label = model.Label,
            Status = model.Status,
            Environment = model.Environment,
            SystemPrompt = model.SystemPrompt,
            ModelProvider = model.ModelProvider,
            ModelId = model.ModelId,
            Tools = model.Tools,
            PolicySnapshotJson = model.PolicySnapshotJson,
            ParametersJson = System.Text.Json.JsonSerializer.Serialize(model.Parameters),
            Description = model.Description,
            ChangeLog = model.ChangeLog,
            CreatedBy = model.CreatedBy,
            CreatedAt = model.CreatedAt,
            PromotedAt = model.PromotedAt,
            PromotedBy = model.PromotedBy,
            ConfigHash = model.ConfigHash,
            ParentVersionId = model.ParentVersionId
        };
    }
}
