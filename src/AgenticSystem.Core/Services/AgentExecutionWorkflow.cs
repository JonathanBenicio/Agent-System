using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Core.Services;

public class AgentExecutionWorkflow : IAgentExecutionWorkflow
{
    private readonly IDirectAgentRequestExecutor _directAgentRequestExecutor;
    private readonly ISessionManager _sessionManager;
    private readonly IAgentRuntimeCoordinator _runtimeCoordinator;
    private readonly ILLMRuntimeContextAccessor _llmRuntimeContextAccessor;
    private readonly IFrameworkOrchestratorService _frameworkOrchestrator;
    private readonly ILogger<AgentExecutionWorkflow> _logger;

    public AgentExecutionWorkflow(
        IDirectAgentRequestExecutor directAgentRequestExecutor,
        ISessionManager sessionManager,
        IAgentRuntimeCoordinator runtimeCoordinator,
        ILLMRuntimeContextAccessor llmRuntimeContextAccessor,
        IFrameworkOrchestratorService frameworkOrchestrator,
        ILogger<AgentExecutionWorkflow> logger)
    {
        _directAgentRequestExecutor = directAgentRequestExecutor;
        _sessionManager = sessionManager;
        _runtimeCoordinator = runtimeCoordinator;
        _llmRuntimeContextAccessor = llmRuntimeContextAccessor;
        _frameworkOrchestrator = frameworkOrchestrator;
        _logger = logger;
    }

    public async Task<AgentResponse> ExecuteAsync(string sessionId, string input, UserContext context, CancellationToken ct = default)
    {
        using var llmScope = _llmRuntimeContextAccessor.BeginScope(context, sessionId);

        try
        {
            _logger.LogInformation("🎯 Workflow executando request: {Input}", input[..Math.Min(50, input.Length)]);
            _logger.LogDebug("Delegating to Framework Orchestrator");
            var orchestratorResponse = await _frameworkOrchestrator.ExecuteAsync(sessionId, input, context, ct);
            return orchestratorResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Workflow execution failed: {Message}", ex.Message);
            await _runtimeCoordinator.PublishEventAsync(new AgentStreamEvent
            {
                Type = AgentStreamEventType.Error,
                Message = ex.Message,
                Data = new Dictionary<string, object>
                {
                    ["fallback"] = false,
                    ["workflow"] = "primary"
                }
            }, ct);

            try { await _sessionManager.EndSessionAsync(sessionId); }
            catch (Exception endEx) { _logger.LogWarning(endEx, "Falha ao finalizar sessão {SessionId}", sessionId); }

            return AgentResponse.Error("Erro interno ao processar a requisição.", "AgentExecutionWorkflow");
        }
    }

    public async Task<AgentResponse> ExecuteDirectAsync(string sessionId, string input, UserContext context, string targetAgent, CancellationToken ct = default)
    {
        using var llmScope = _llmRuntimeContextAccessor.BeginScope(context, sessionId);
        return await _directAgentRequestExecutor.ExecuteAsync(sessionId, input, context, targetAgent, ct);
    }
}
