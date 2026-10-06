using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AgenticSystem.Core.Interfaces;

namespace AgenticSystem.Infrastructure.Persistence.Entities;

/// <summary>
/// Entidade de banco de dados representando a especificação de um agente inteligente criado dinamicamente, persistida no PostgreSQL.
/// Suporta multi-tenancy rigoroso através da interface ITenantEntity.
/// </summary>
[Table("dynamic_agents")]
public class DynamicAgentEntity : ITenantEntity
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [Required]
    public string TenantId { get; set; } = string.Empty;

    [Required]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string Domain { get; set; } = string.Empty;

    public int Tier { get; set; } // Representará AgenticSystem.Core.Models.AgentTier (Support = 3, Specialist = 2, Master = 1, Chief = 0)

    [Required]
    public string Instructions { get; set; } = string.Empty;

    public int AutonomyLevel { get; set; } // Representará AutonomyLevel (None = 0, Monitored = 1, Autonomous = 2)

    public string? AllowedToolsJson { get; set; }

    [Column(TypeName = "jsonb")]
    public string? SpecificationJson { get; set; }

    [Required]
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
