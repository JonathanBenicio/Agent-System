using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Interfaces;

/// <summary>
/// Executa o caminho direto de um agente usando infraestrutura opcional,
/// sem envolver wrappers transitórios de IAgent.
/// </summary>
public interface IDirectAgentExecutionService
{
    Task<AgentResponse> ExecuteDirectAsync(
        IAgent agent,
        string sessionId,
        string input,
        UserContext context,
        CancellationToken ct = default);
}