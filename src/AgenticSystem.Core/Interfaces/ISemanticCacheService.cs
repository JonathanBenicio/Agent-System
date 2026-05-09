using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Interfaces;

/// <summary>
/// Serviço de Cache Semântico para interceptação de intenções baseadas em similaridade vetorial.
/// </summary>
public interface ISemanticCacheService
{
    /// <summary>
    /// Procura no cache por uma requisição semanticamente similar.
    /// </summary>
    Task<SemanticCacheResult> GetCachedResponseAsync(string prompt, string agentName, double similarityThreshold = 0.95, CancellationToken ct = default);

    /// <summary>
    /// Armazena a resposta no cache vetorial.
    /// </summary>
    Task SetCachedResponseAsync(string prompt, string response, string agentName, TimeSpan? ttl = null, CancellationToken ct = default);

    /// <summary>
    /// Invalida caches baseados no Agente.
    /// </summary>
    Task InvalidateAgentCacheAsync(string agentName, CancellationToken ct = default);
}
