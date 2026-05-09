using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Interfaces;

/// <summary>
/// Meta-Agent: Componente central que analisa contexto, roteia para agents especializados
/// e gerencia sessões. Inspirado no Tech Lead do Labs.
/// </summary>
public interface IMetaAgent
{
    /// <summary>
    /// Processa uma requisição do usuário através do pipeline completo:
    /// 1. Análise de contexto
    /// 2. Seleção/Criação de agent
    /// 3. Execução com tracking
    /// 4. Consolidação na memória
    /// </summary>
    Task<AgentResponse> ProcessRequestAsync(string input, UserContext context);

    IAsyncEnumerable<AgentStreamEvent> ProcessRequestStreamAsync(string input, UserContext context, CancellationToken ct = default);

    /// <summary>
    /// Processa uma requisição direcionada a um agent específico, 
    /// bypassing a análise de contexto e seleção automática do MetaAgent.
    /// </summary>
    Task<AgentResponse> ProcessDirectRequestAsync(string input, UserContext context, string targetAgent);

    IAsyncEnumerable<AgentStreamEvent> ProcessDirectRequestStreamAsync(string input, UserContext context, string targetAgent, CancellationToken ct = default);
    
    /// <summary>
    /// Lista todos os agents ativos no sistema
    /// </summary>
    Task<IEnumerable<AgentInfo>> GetActiveAgentsAsync();
    
    /// <summary>
    /// Força limpeza de agents inativos (Tier 3 principalmente)
    /// </summary>
    Task CleanupInactiveAgentsAsync();
}