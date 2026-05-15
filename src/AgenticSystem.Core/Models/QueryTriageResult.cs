namespace AgenticSystem.Core.Models.Triage;

/// <summary>
/// Nível de complexidade da requisição para triagem rápida
/// </summary>
public enum ComplexityLevel
{
    Low,
    Medium,
    High
}

/// <summary>
/// Tipo de intenção identificada durante a triagem
/// </summary>
public enum IntentType
{
    SmallTalk,
    DirectAnswer,
    ComplexReasoning
}

/// <summary>
/// Resultado da triagem semântica de uma requisição
/// </summary>
public class QueryTriageResult
{
    public IntentType Intent { get; set; }
    public ComplexityLevel Complexity { get; set; }
    public bool RequiresRAG { get; set; }
    public bool RequiresTools { get; set; }
    public string RecommendedAgentTier { get; set; } = string.Empty;
    public string EstimatedAgent { get; set; } = string.Empty;
}

