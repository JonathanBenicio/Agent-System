using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Infrastructure.Persistence.Entities;

[Table("prompt_templates")]
public class PromptTemplateEntity
{
    [Key]
    [MaxLength(64)]
    public string Id { get; set; } = string.Empty;

    [Required]
    [MaxLength(256)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(256)]
    public string AgentName { get; set; } = string.Empty;

    [Required]
    public string TemplateBody { get; set; } = string.Empty;

    public int Version { get; set; }

    [Required]
    [MaxLength(16)]
    public string Locale { get; set; } = "pt-BR";

    [Required]
    public List<string> Variables { get; set; } = new();

    public string? Description { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    [MaxLength(128)]
    public string? CreatedBy { get; set; }

    public PromptTemplate ToModel()
    {
        return new PromptTemplate
        {
            Id = Id,
            Name = Name,
            AgentName = AgentName,
            TemplateBody = TemplateBody,
            Version = Version,
            Locale = Locale,
            Variables = Variables,
            Description = Description,
            IsActive = IsActive,
            CreatedAt = CreatedAt,
            UpdatedAt = UpdatedAt,
            CreatedBy = CreatedBy
        };
    }

    public static PromptTemplateEntity FromModel(PromptTemplate model)
    {
        return new PromptTemplateEntity
        {
            Id = model.Id,
            Name = model.Name,
            AgentName = model.AgentName,
            TemplateBody = model.TemplateBody,
            Version = model.Version,
            Locale = model.Locale,
            Variables = model.Variables,
            Description = model.Description,
            IsActive = model.IsActive,
            CreatedAt = model.CreatedAt,
            UpdatedAt = model.UpdatedAt,
            CreatedBy = model.CreatedBy
        };
    }
}
