namespace AgenticSystem.Core.Models;

/// <summary>
/// Resposta padronizada de qualquer agent do sistema
/// </summary>
public class AgentResponse
{
    /// <summary>
    /// Conteúdo da resposta
    /// </summary>
    public string Content { get; set; } = string.Empty;
    
    /// <summary>
    /// Agent que processou a requisição
    /// </summary>
    public string AgentName { get; set; } = string.Empty;
    
    /// <summary>
    /// Tier do agent
    /// </summary>
    public AgentTier AgentTier { get; set; }
    
    /// <summary>
    /// Ações executadas durante o processamento
    /// </summary>
    public List<string> ActionsPerformed { get; set; } = new();
    
    /// <summary>
    /// Tools utilizadas
    /// </summary>
    public List<string> ToolsUsed { get; set; } = new();
    
    /// <summary>
    /// Indica se houve sucesso
    /// </summary>
    public bool Success { get; set; } = true;
    
    /// <summary>
    /// Mensagem de erro se houver
    /// </summary>
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// Metadados adicionais
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
    
    /// <summary>
    /// Timestamp da resposta
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Seção da conversa (para tracking)
    /// </summary>
    public string? SessionId { get; set; }
    
    /// <summary>
    /// Sugere handoffs para outros agents
    /// </summary>
    public List<HandoffSuggestion> SuggestedHandoffs { get; set; } = new();

    /// <summary>
    /// Score de confiança exposto ao usuário (Maturity Level 7)
    /// </summary>
    public ConfidenceScore? Confidence { get; set; }
    
    public static AgentResponse Ok(string content, string agentName, AgentTier tier)
    {
        return new AgentResponse
        {
            Content = content,
            AgentName = agentName,
            AgentTier = tier,
            Success = true
        };
    }
    
    public static AgentResponse Error(string errorMessage, string agentName = "System")
    {
        return new AgentResponse
        {
            Content = $"Erro: {errorMessage}",
            AgentName = agentName,
            Success = false,
            ErrorMessage = errorMessage
        };
    }
}

/// <summary>
/// Sugestão de handoff para outro agent
/// </summary>
public class HandoffSuggestion
{
    public string TargetAgent { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
    public Dictionary<string, object> Context { get; set; } = new();
}