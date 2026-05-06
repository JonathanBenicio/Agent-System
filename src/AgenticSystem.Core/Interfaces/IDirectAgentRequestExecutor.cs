using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Interfaces;

/// <summary>
/// Executa o caminho direto de um agent nomeado, mantendo o workflow principal fino.
/// </summary>
public interface IDirectAgentRequestExecutor
{
    Task<AgentResponse> ExecuteAsync(
        string sessionId,
        string input,
        UserContext context,
        string targetAgent,
        CancellationToken ct = default);
}