using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Interfaces;

/// <summary>
/// Interface base para todos os agents do sistema.
/// Cada agent tem um tier, tools permitidas e especialização.
/// </summary>
public interface IAgent
{
    string Name { get; }
    string Description { get; }
    AgentTier Tier { get; }
    string Domain { get; }
    DateTime CreatedAt { get; }
    DateTime LastUsedAt { get; }
    bool IsActive { get; }
    
    /// <summary>
    /// Executa uma requisição dentro do escopo do agent
    /// </summary>
    Task<AgentResponse> ExecuteAsync(string input, UserContext context);
    
    /// <summary>
    /// Verifica se o agent pode processar a requisição
    /// </summary>
    Task<bool> CanHandleAsync(AnalysisResult analysis);
    
    /// <summary>
    /// Tools disponíveis para este agent
    /// </summary>
    IEnumerable<string> AvailableTools { get; }

    /// <summary>
    /// System prompt / instruções base do agent.
    /// Usado pelo Agent Framework para configurar o ChatClientAgent.
    /// </summary>
    string Instructions { get; }
    
    /// <summary>
    /// Atualiza última utilização (para cleanup)
    /// </summary>
    void UpdateLastUsed();
}