using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Interfaces;

/// <summary>
/// ML11 — Serviço de criação dinâmica de agents via linguagem natural.
/// Detecta intent CreateAgent, gera AgentSpecification via LLM e cria o agent.
/// </summary>
public interface IDynamicAgentService
{
    /// <summary>
    /// Detecta se o input é um pedido de criação de agent
    /// </summary>
    Task<bool> IsAgentCreationRequestAsync(string input, AnalysisResult analysis);

    /// <summary>
    /// Gera AgentSpecification a partir de linguagem natural via LLM
    /// </summary>
    Task<AgentSpecification> GenerateSpecificationAsync(string input, UserContext context);

    /// <summary>
    /// Pipeline completo: detecta, gera spec, cria agent e retorna resposta
    /// </summary>
    Task<AgentResponse> HandleAgentCreationAsync(string input, UserContext context);

    /// <summary>
    /// Lista agents dinâmicos (custom) criados pelo usuário
    /// </summary>
    Task<IEnumerable<AgentInfo>> GetDynamicAgentsAsync(string? userId = null);

    /// <summary>
    /// Remove um agent dinâmico
    /// </summary>
    Task<bool> RemoveAgentAsync(string agentName);
}
