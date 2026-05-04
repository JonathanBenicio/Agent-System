namespace AgenticSystem.Core.Models;

/// <summary>
/// Resultado da análise de contexto pelo ContextAnalyzer
/// </summary>
public class AnalysisResult
{
    /// <summary>
    /// Intenção identificada na requisição
    /// </summary>
    public IntentType Intent { get; set; }
    
    /// <summary>
    /// Domínio principal (personal, work, learning, etc.)
    /// </summary>
    public string PrimaryDomain { get; set; } = string.Empty;
    
    /// <summary>
    /// Domínios secundários se aplicável
    /// </summary>
    public List<string> SecondaryDomains { get; set; } = new();
    
    /// <summary>
    /// Complexidade estimada
    /// </summary>
    public ComplexityLevel Complexity { get; set; }
    
    /// <summary>
    /// Prioridade (urgency)
    /// </summary>
    public Priority Priority { get; set; }
    
    /// <summary>
    /// Agent estimado para processar
    /// </summary>
    public string EstimatedAgent { get; set; } = string.Empty;
    
    /// <summary>
    /// Tier recomendado
    /// </summary>
    public AgentTier RecommendedTier { get; set; }
    
    /// <summary>
    /// Tools necessárias
    /// </summary>
    public List<string> RequiredTools { get; set; } = new();
    
    /// <summary>
    /// Contexto adicional identificado
    /// </summary>
    public Dictionary<string, object> ExtractedContext { get; set; } = new();
    
    /// <summary>
    /// Confiança da análise (0.0 a 1.0)
    /// </summary>
    public double Confidence { get; set; }
    
    /// <summary>
    /// Indica se precisa de delegação para múltiplos agents
    /// </summary>
    public bool RequiresDelegation { get; set; }
}

/// <summary>
/// Prioridade da requisição
/// </summary>
public enum Priority
{
    Low,
    Medium,
    High,
    Immediate
}