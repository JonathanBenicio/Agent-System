using AgenticSystem.Core.Interfaces;

namespace AgenticSystem.Core.Models;

/// <summary>
/// Registro de uma variante/versionamento de tool sob um logical tool id.
/// </summary>
public class ToolRegistration
{
    public string LogicalToolId { get; set; } = string.Empty;
    public string Version { get; set; } = "1.0.0";
    public string? VariantName { get; set; }
    public int RolloutPercentage { get; set; } = 100;
    public bool IsDefault { get; set; }
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
    public ITool Tool { get; set; } = null!;

    public string RegistrationKey => $"{Version}:{VariantName ?? "default"}";
}