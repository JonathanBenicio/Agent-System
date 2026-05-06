using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using AgenticSystem.Core.Interfaces;

namespace AgenticSystem.Infrastructure.AgentFramework;

/// <summary>
/// Materializa e expõe o catálogo estável de tools auxiliares do orquestrador.
/// </summary>
public class OrchestratorAuxiliaryToolService
{
    private readonly IReadOnlyList<AITool> _tools;

    public OrchestratorAuxiliaryToolService(
        ILogger<OrchestratorAuxiliaryToolService> logger,
        IRAGService? ragService = null,
        IContextBudgetManager? contextBudgetManager = null,
        ISmartRouter? smartRouter = null,
        IContextAnalyzer? contextAnalyzer = null)
    {
        ArgumentNullException.ThrowIfNull(logger);

        var tools = new List<AITool>();

        if (ragService is not null)
        {
            tools.Add(
                OrchestratorAuxiliaryTools.CreateRetrieveContextTool(ragService, contextBudgetManager));
        }

        if (smartRouter is not null)
        {
            tools.Add(
                OrchestratorAuxiliaryTools.CreateRouteToAgentTool(smartRouter));
        }

        if (contextAnalyzer is not null)
        {
            tools.Add(
                OrchestratorAuxiliaryTools.CreateAnalyzeRequestTool(contextAnalyzer));
        }

        _tools = tools;

        if (_tools.Count > 0)
        {
            logger.LogInformation(
                "Orchestrator auxiliary tools registered: {Tools}",
                string.Join(", ", _tools.Select(t => t.Name)));
        }
    }

    public IReadOnlyList<AITool> GetTools() => _tools;
}