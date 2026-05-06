using Microsoft.Extensions.Logging;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Infrastructure.AgentFramework;

/// <summary>
/// Cria um AgentFrameworkAdapter explicitamente para o caminho de execução direta.
/// Não decora mais o IAgentFactory global, preservando agentes crus para tool bindings
/// e para os fluxos já framework-first.
/// </summary>
public class AgentFrameworkAgentFactory : IDirectAgentExecutionFactory
{
    private readonly AgentFrameworkFactory _frameworkFactory;
    private readonly AgentFrameworkSessionStoreAdapter _sessionStore;
    private readonly ISessionManager _sessionManager;
    private readonly ILogger<AgentFrameworkAdapter> _adapterLogger;
    private readonly IAgentRuntimeCoordinator? _runtimeCoordinator;
    private readonly bool _enableStreaming;

    public AgentFrameworkAgentFactory(
        AgentFrameworkFactory frameworkFactory,
        AgentFrameworkSessionStoreAdapter sessionStore,
        ISessionManager sessionManager,
        ILogger<AgentFrameworkAdapter> adapterLogger,
        IAgentRuntimeCoordinator? runtimeCoordinator = null,
        bool enableStreaming = false)
    {
        _frameworkFactory = frameworkFactory ?? throw new ArgumentNullException(nameof(frameworkFactory));
        _sessionStore = sessionStore ?? throw new ArgumentNullException(nameof(sessionStore));
        _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
        _adapterLogger = adapterLogger ?? throw new ArgumentNullException(nameof(adapterLogger));
        _runtimeCoordinator = runtimeCoordinator;
        _enableStreaming = enableStreaming;
    }

    public async Task<IAgent> CreateDirectExecutionAgentAsync(IAgent agent, CancellationToken ct = default)
    {
        if (agent is AgentFrameworkAdapter)
        {
            return agent;
        }

        var frameworkAgent = await _frameworkFactory.CreateFromAgentAsync(agent, ct);
        return new AgentFrameworkAdapter(
            agent,
            frameworkAgent,
            _sessionStore,
            _sessionManager,
            _adapterLogger,
            _runtimeCoordinator,
            _enableStreaming);
    }
}
