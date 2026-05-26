using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AgenticSystem.Core.Interfaces;

namespace AgenticSystem.Infrastructure.Persistence.Entities;

/// <summary>
/// Entidade de banco de dados representando uma ferramenta (Tool) de agentes inteligente persistida no PostgreSQL.
/// Suporta multi-tenancy rigoroso através da interface ITenantEntity.
/// </summary>
[Table("agent_tools")]
public class DbToolEntity : ITenantEntity
{
    [Key]
    public string Id { get; set; } = string.Empty;

    [Required]
    public string TenantId { get; set; } = string.Empty;

    [Required]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string Category { get; set; } = "Api";

    [Required]
    public bool RequiresAuth { get; set; } = false;

    [Required]
    [MaxLength(50)]
    public string Type { get; set; } = "Custom"; // Custom, Mcp, Builtin

    public string MetadataJson { get; set; } = "{}";

    [Required]
    [MaxLength(50)]
    public string Version { get; set; } = "1.0.0";

    [MaxLength(50)]
    public string? VariantName { get; set; }

    [Required]
    public int RolloutPercentage { get; set; } = 100;

    [Required]
    public bool IsDefault { get; set; } = true;

    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
