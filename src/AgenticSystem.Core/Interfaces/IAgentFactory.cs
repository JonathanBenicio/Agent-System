using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Interfaces;

/// <summary>
/// Factory para criação dinâmica de agents baseado em contexto.
/// Implementa Tier System do Baianinho-Labs (0=Chief, 1=Master, 2=Specialist, 3=Support)
/// </summary>
public interface IAgentFactory
{
    /// <summary>
    /// Obtém agent existente ou cria novo baseado no contexto
    /// </summary>
    Task<IAgent> GetOrCreateAgentAsync(AnalysisResult context);
    
    /// <summary>
    /// Cria agent customizado para domínio específico
    /// </summary>
    Task<IAgent> CreateCustomAgentAsync(AgentSpecification specification);
    
    /// <summary>
    /// Determina tier apropriado baseado na complexidade
    /// </summary>
    AgentTier DetermineTier(ComplexityLevel complexity);
    
    /// <summary>
    /// Lista agents disponíveis por tier
    /// </summary>
    Task<IEnumerable<AgentInfo>> GetAgentsByTierAsync(AgentTier tier);

    /// <summary>
    /// Lista todos os agents do pool (incluindo dinâmicos)
    /// </summary>
    Task<IEnumerable<AgentInfo>> GetAllAgentsAsync();

    /// <summary>
    /// Remove um agent do pool
    /// </summary>
    Task<bool> RemoveAgentAsync(string agentName);
}