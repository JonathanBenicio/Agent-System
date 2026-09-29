using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AgenticSystem.Core.Interfaces;

namespace AgenticSystem.Infrastructure.Persistence.Entities;

/// <summary>
/// Entidade de banco de dados representando uma Skill (habilidade) de agentes inteligente persistida no PostgreSQL.
/// Suporta multi-tenancy rigoroso através da interface ITenantEntity.
/// </summary>
[Table("agent_skills")]
public class DbSkillEntity : ITenantEntity
{
    [Key]
    public string Id { get; set; } = string.Empty; // ex: "coding-assistant", "custom-resumidor"

    [Required]
    public string TenantId { get; set; } = string.Empty; // Segregação multi-tenant estrita

    [Required]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string Domain { get; set; } = "general"; // work, personal, general

    [Required]
    [MaxLength(50)]
    public string Type { get; set; } = "Instruction"; // Instruction, Knowledge, Template

    [Required]
    public string SystemPromptFragment { get; set; } = string.Empty;

    public string? FewShotExamples { get; set; }

    [Required]
    public bool IsSystem { get; set; } = false; // TRUE = Semeada pelo sistema (Read-Only)
    public bool IsEnabled { get; set; } = true;

    public string? MetadataJson { get; set; }

    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
