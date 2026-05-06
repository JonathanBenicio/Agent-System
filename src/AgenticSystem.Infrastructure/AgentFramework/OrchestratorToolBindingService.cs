using Microsoft.Extensions.Logging;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Infrastructure.AgentFramework;

/// <summary>
/// Cria os tool bindings dos especialistas para uma sessão específica do orquestrador.
/// </summary>
public class OrchestratorToolBindingService(
    IAgentFactory agentFactory,
    AgentFrameworkFactory frameworkFactory,
    ILogger<OrchestratorToolBindingService> logger)
{
    private readonly IAgentFactory _agentFactory = agentFactory ?? throw new ArgumentNullException(nameof(agentFactory));
    private readonly AgentFrameworkFactory _frameworkFactory = frameworkFactory ?? throw new ArgumentNullException(nameof(frameworkFactory));
    private readonly ILogger<OrchestratorToolBindingService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <summary>
    /// Cria os bindings dos especialistas ativos para a sessão informada.
    /// </summary>
    public async Task<List<AgentToolBinding>> CreateSpecialistBindingsAsync(
        IReadOnlyList<AgentInfo> activeAgentInfos,
        string sessionId,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(activeAgentInfos);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        var bindings = new List<AgentToolBinding>();

        foreach (var info in activeAgentInfos)
        {
            try
            {
                var agent = await _agentFactory.ResolveAgentAsync(info);
                var binding = await _frameworkFactory.CreateToolBindingAsync(agent, sessionId, ct);

                if (binding is not null)
                {
                    bindings.Add(binding);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to create tool binding for agent {Agent}, skipping",
                    info.Name);
            }
        }

        return bindings;
    }
}