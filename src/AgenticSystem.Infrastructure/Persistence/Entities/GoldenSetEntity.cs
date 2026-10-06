using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Infrastructure.Persistence.Entities;

[Table("golden_sets")]
public class GoldenSetEntity : ITenantEntity
{
    [Key]
    [MaxLength(50)]
    public string Id { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string TenantId { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string AgentName { get; set; } = string.Empty;

    [Required]
    public string CasesJson { get; set; } = "[]";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public GoldenSet ToModel()
    {
        return new GoldenSet
        {
            Id = Id,
            TenantId = TenantId,
            Name = Name,
            Description = Description,
            AgentName = AgentName,
            Cases = string.IsNullOrWhiteSpace(CasesJson)
                ? new List<GoldenSetCase>()
                : System.Text.Json.JsonSerializer.Deserialize<List<GoldenSetCase>>(CasesJson) ?? new(),
            CreatedAt = CreatedAt,
            UpdatedAt = UpdatedAt
        };
    }

    public static GoldenSetEntity FromModel(GoldenSet model)
    {
        return new GoldenSetEntity
        {
            Id = model.Id,
            TenantId = model.TenantId,
            Name = model.Name,
            Description = model.Description,
            AgentName = model.AgentName,
            CasesJson = System.Text.Json.JsonSerializer.Serialize(model.Cases),
            CreatedAt = model.CreatedAt,
            UpdatedAt = model.UpdatedAt
        };
    }
}
