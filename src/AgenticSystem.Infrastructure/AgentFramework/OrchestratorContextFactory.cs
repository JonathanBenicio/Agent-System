using AgenticSystem.Core.Interfaces;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Logging;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Infrastructure.AgentFramework;

/// <summary>
/// [DEPRECATED] Adapter transitório que encapsula OrchestratorHostBuilder.
/// Mantido para compatibilidade com DI existente; remover após consolidação completa.
/// Use OrchestratorHostBuilder diretamente para novos fluxos.
/// </summary>
public class OrchestratorContextFactory
{
    private readonly OrchestratorHostBuilder _hostBuilder;
    private readonly IAgentFactory _agentFactory;
    private readonly ILLMRuntimeContextAccessor _runtimeContextAccessor;
    private readonly ILogger<OrchestratorContextFactory> _logger;

    public OrchestratorContextFactory(
        OrchestratorHostBuilder hostBuilder,
        IAgentFactory agentFactory,
        ILLMRuntimeContextAccessor runtimeContextAccessor,
        ILogger<OrchestratorContextFactory> logger)
    {
        _hostBuilder = hostBuilder ?? throw new ArgumentNullException(nameof(hostBuilder));
        _agentFactory = agentFactory ?? throw new ArgumentNullException(nameof(agentFactory));
        _runtimeContextAccessor = runtimeContextAccessor ?? throw new ArgumentNullException(nameof(runtimeContextAccessor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Resolve o orquestrador de forma síncrona (compatibilidade DI).
    /// [DEPRECATED] Use OrchestratorHostBuilder.Build() diretamente.
    /// </summary>
    public OrchestratorContext Resolve()
    {
        return ResolveAsync(CancellationToken.None)
            .GetAwaiter()
            .GetResult();
    }

    /// <summary>
    /// Resolve o orquestrador com contexto de sessão ativa.
    /// </summary>
    public async Task<OrchestratorContext> ResolveAsync(CancellationToken ct = default)
    {
        var activeAgents = await GetActiveAgentsAsync();
        var orchestratorAgent = await _hostBuilder.BuildAsync(activeAgents, ct);

        _logger.LogDebug(
            "Orchestrator context resolved with {AgentCount} active specialists",
            activeAgents.Count);

        // OrchestratorContext retorna agent + empty bindings (bindings são internos ao agent agora)
        return new OrchestratorContext(orchestratorAgent, []);
    }

    /// <summary>
    /// Invalida cache de instruções quando especialistas mudam.
    /// </summary>
    public void Invalidate()
    {
        _logger.LogInformation("Orchestrator context factory cache invalidated");
    }

    private async Task<List<AgentInfo>> GetActiveAgentsAsync()
    {
        return (await _agentFactory.GetAllAgentsAsync())
            .Where(agent => agent.IsActive)
            .ToList();
    }
}