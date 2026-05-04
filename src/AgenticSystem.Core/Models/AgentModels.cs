namespace AgenticSystem.Core.Models;

/// <summary>
/// Informações sobre um agent do sistema
/// </summary>
public class AgentInfo
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public AgentTier Tier { get; set; }
    public string Domain { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime LastUsedAt { get; set; }
    public bool IsActive { get; set; }
    public List<string> AvailableTools { get; set; } = new();
    public Dictionary<string, object> Configuration { get; set; } = new();
}

/// <summary>
/// Especificação para criação de agent dinâmico
/// </summary>
public class AgentSpecification
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public AgentTier Tier { get; set; }
    public string Domain { get; set; } = string.Empty;
    public List<string> AllowedTools { get; set; } = new();
    public string Instructions { get; set; } = string.Empty;
    public Dictionary<string, object> Configuration { get; set; } = new();
    public TimeSpan? AutoCleanupAfter { get; set; }
}

/// <summary>
/// Evento de interação com agent (para memória)
/// </summary>
public class AgentEvent
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string SessionId { get; set; } = string.Empty;
    public string AgentName { get; set; } = string.Empty;
    public AgentTier AgentTier { get; set; }
    public string UserInput { get; set; } = string.Empty;
    public string AgentResponse { get; set; } = string.Empty;
    public List<string> ActionsPerformed { get; set; } = new();
    public List<string> ToolsUsed { get; set; } = new();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public Dictionary<string, object> Context { get; set; } = new();
    public List<string> Tags { get; set; } = new();
    public List<string> RelatedEvents { get; set; } = new();
}