namespace AgenticSystem.Core.Interfaces;

/// <summary>
/// Cria um agente executável para o caminho direto, sem contaminar os demais
/// consumidores de IAgentFactory com wrappers de infraestrutura.
/// </summary>
public interface IDirectAgentExecutionFactory
{
    Task<IAgent> CreateDirectExecutionAgentAsync(IAgent agent, CancellationToken ct = default);
}