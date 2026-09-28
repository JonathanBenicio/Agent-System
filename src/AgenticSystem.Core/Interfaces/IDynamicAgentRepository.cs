using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Interfaces;

/// <summary>
/// Repositório para persistência de agentes criados dinamicamente via chat/UI.
/// </summary>
public interface IDynamicAgentRepository
{
    /// <summary>
    /// Recupera todas as especificações de agentes dinâmicos para o tenant atual.
    /// </summary>
    Task<IEnumerable<AgentSpecification>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Recupera a especificação de um agente dinâmico pelo nome.
    /// </summary>
    Task<AgentSpecification?> GetByNameAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Salva ou atualiza uma especificação de agente dinâmico.
    /// </summary>
    Task SaveAsync(AgentSpecification specification, CancellationToken cancellationToken = default);

    /// <summary>
    /// Desativa um agente dinâmico pelo nome.
    /// </summary>
    Task<bool> DeactivateAsync(string name, CancellationToken cancellationToken = default);
}
