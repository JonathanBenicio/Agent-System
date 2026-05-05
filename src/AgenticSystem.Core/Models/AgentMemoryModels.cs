namespace AgenticSystem.Core.Models;

/// <summary>
/// Tipos de memória persistente por agente.
/// </summary>
public enum AgentMemoryType
{
    Preference,
    LearnedRule,
    Correction,
    Fact,
    Reflection
}

/// <summary>
/// Memória persistente que um agente pode reutilizar entre sessões.
/// </summary>
public class AgentMemoryEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string UserId { get; set; } = string.Empty;
    public string AgentName { get; set; } = string.Empty;
    public string? SessionId { get; set; }
    public AgentMemoryType MemoryType { get; set; } = AgentMemoryType.Fact;
    public string Content { get; set; } = string.Empty;
    public double Confidence { get; set; } = 0.5;
    public string Source { get; set; } = "runtime";
    public List<string> Keywords { get; set; } = new();
    public Dictionary<string, string> Metadata { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastUsedAt { get; set; } = DateTime.UtcNow;
    public int UsageCount { get; set; }
    public bool IsActive { get; set; } = true;
}