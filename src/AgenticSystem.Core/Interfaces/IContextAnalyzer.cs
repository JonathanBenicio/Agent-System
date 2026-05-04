using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Interfaces;

/// <summary>
/// Analisa intenção, domínio e complexidade de requisições.
/// Baseado nas Instructions contextuais do Baianinho-Labs.
/// </summary>
public interface IContextAnalyzer
{
    /// <summary>
    /// Analisa uma requisição e retorna contexto estruturado para decisão de routing
    /// </summary>
    Task<AnalysisResult> AnalyzeAsync(string input, UserContext userContext);
    
    /// <summary>
    /// Extrai entidades e conceitos principais de uma requisição
    /// </summary>
    Task<List<ExtractedEntity>> ExtractEntitiesAsync(string input);
    
    /// <summary>
    /// Determina se a requisição precisa de múltiplos agents (delegação)
    /// </summary>
    Task<bool> RequiresDelegationAsync(AnalysisResult analysis);
}