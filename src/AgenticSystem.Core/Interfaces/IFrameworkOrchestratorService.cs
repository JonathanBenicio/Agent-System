using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Interfaces;

/// <summary>
/// Serviço de orquestração centralizado no Microsoft Agent Framework.
/// O orquestrador é um ChatClientAgent cujo LLM decide qual especialista chamar
/// via tool bindings, substituindo a lógica imperativa de roteamento.
/// </summary>
public interface IFrameworkOrchestratorService
{
    Task<AgentResponse> ExecuteAsync(
        string sessionId,
        string input,
        UserContext context,
        CancellationToken ct = default);
}
