using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Interfaces;

/// <summary>
/// Factory para criação dinâmica de agents baseado em contexto.
/// Implementa Tier System do Baianinho-Labs (0=Chief, 1=Master, 2=Specialist, 3=Support)
/// </summary>
public interface IAgentFactory
{
    /// <summary>
    /// Resolve um agent existente ou cria um novo a partir de uma decisão já tomada.
    /// Não executa orquestração nem escolha de especialista; apenas materializa o agent.
    /// </summary>
    Task<IAgent> ResolveAgentAsync(AnalysisResult context);

    /// <summary>
    /// Resolve um agent a partir de metadados já materializados no catálogo ativo.
    /// </summary>
    Task<IAgent> ResolveAgentAsync(AgentInfo agentInfo);
    
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