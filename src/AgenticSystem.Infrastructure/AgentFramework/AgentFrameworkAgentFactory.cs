using Microsoft.Extensions.Logging;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Infrastructure.AgentFramework;

/// <summary>
/// Decorator de IAgentFactory que envolve agents retornados com AgentFrameworkAdapter,
/// conectando o pipeline do Microsoft Agent Framework (logging + telemetry + IChatClient)
/// ao fluxo real de execução do MetaAgentOrchestrator.
/// </summary>
public class AgentFrameworkAgentFactory : IAgentFactory
{
    private readonly IAgentFactory _inner;
    private readonly AgentFrameworkFactory _frameworkFactory;
    private readonly AgentSessionBridge _sessionBridge;
    private readonly ILogger<AgentFrameworkAdapter> _adapterLogger;
    private readonly IAgentRuntimeCoordinator? _runtimeCoordinator;
    private readonly bool _enableStreaming;

    public AgentFrameworkAgentFactory(
        IAgentFactory inner,
        AgentFrameworkFactory frameworkFactory,
        AgentSessionBridge sessionBridge,
        ILogger<AgentFrameworkAdapter> adapterLogger,
        IAgentRuntimeCoordinator? runtimeCoordinator = null,
        bool enableStreaming = false)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _frameworkFactory = frameworkFactory ?? throw new ArgumentNullException(nameof(frameworkFactory));
        _sessionBridge = sessionBridge ?? throw new ArgumentNullException(nameof(sessionBridge));
        _adapterLogger = adapterLogger ?? throw new ArgumentNullException(nameof(adapterLogger));
        _runtimeCoordinator = runtimeCoordinator;
        _enableStreaming = enableStreaming;
    }

    public async Task<IAgent> GetOrCreateAgentAsync(AnalysisResult context)
    {
        var agent = await _inner.GetOrCreateAgentAsync(context);
        return await WrapWithFrameworkAsync(agent);
    }

    public async Task<IAgent> CreateCustomAgentAsync(AgentSpecification specification)
    {
        var agent = await _inner.CreateCustomAgentAsync(specification);
        return await WrapWithFrameworkAsync(agent);
    }

    public AgentTier DetermineTier(ComplexityLevel complexity)
        => _inner.DetermineTier(complexity);

    public Task<IEnumerable<AgentInfo>> GetAgentsByTierAsync(AgentTier tier)
        => _inner.GetAgentsByTierAsync(tier);

    public Task<IEnumerable<AgentInfo>> GetAllAgentsAsync()
        => _inner.GetAllAgentsAsync();

    public Task<bool> RemoveAgentAsync(string agentName)
        => _inner.RemoveAgentAsync(agentName);

    private async Task<IAgent> WrapWithFrameworkAsync(IAgent agent)
    {
        if (agent is AgentFrameworkAdapter)
            return agent;

        var frameworkAgent = await _frameworkFactory.CreateFromAgentAsync(agent);
        return new AgentFrameworkAdapter(agent, frameworkAgent, _sessionBridge, _adapterLogger, _runtimeCoordinator, _enableStreaming);
    }
}
