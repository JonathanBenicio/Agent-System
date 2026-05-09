using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Services;

/// <summary>
/// Meta-Agent principal: analisa contexto, roteia para agents e gerencia sessões.
/// Inspirado no Tech Lead do Labs com capacidade de orquestração.
/// </summary>
public class MetaAgentOrchestrator : IMetaAgent
{
    private readonly IFrameworkOrchestratorService _frameworkOrchestrator;
    private readonly IDirectAgentRequestExecutor _directAgentRequestExecutor;
    private readonly ILLMRuntimeContextAccessor _llmRuntimeContextAccessor;
    private readonly IAgentFactory _agentFactory;
    private readonly ISessionManager _sessionManager;
    private readonly IAgentRuntimeCoordinator _runtimeCoordinator;
    private readonly ITenantIsolationEnforcer? _isolationEnforcer;
    private readonly ILogger<MetaAgentOrchestrator> _logger;

    public MetaAgentOrchestrator(
        IFrameworkOrchestratorService frameworkOrchestrator,
        IDirectAgentRequestExecutor directAgentRequestExecutor,
        ILLMRuntimeContextAccessor llmRuntimeContextAccessor,
        IAgentFactory agentFactory,
        ISessionManager sessionManager,
        IAgentRuntimeCoordinator runtimeCoordinator,
        ILogger<MetaAgentOrchestrator> logger,
        ITenantIsolationEnforcer? isolationEnforcer = null)
    {
        _frameworkOrchestrator = frameworkOrchestrator;
        _directAgentRequestExecutor = directAgentRequestExecutor;
        _llmRuntimeContextAccessor = llmRuntimeContextAccessor;
        _agentFactory = agentFactory;
        _sessionManager = sessionManager;
        _runtimeCoordinator = runtimeCoordinator;
        _logger = logger;
        _isolationEnforcer = isolationEnforcer;
    }

    public async Task<AgentResponse> ProcessRequestAsync(string input, UserContext context)
    {
        if (_isolationEnforcer != null && !string.IsNullOrEmpty(context.TenantId))
        {
            if (!await _isolationEnforcer.CanStartSessionAsync(context.TenantId))
            {
                return AgentResponse.Error("🚫 Limite de sessões simultâneas atingido para o seu tenant.");
            }
        }

        var sessionId = await _sessionManager.StartSessionAsync(context);
        context.Preferences["sessionId"] = sessionId;
        using var scope = _runtimeCoordinator.BeginExecutionScope(sessionId, context);
        return await ProcessRequestCoreAsync(sessionId, input, context, CancellationToken.None);
    }

    public async IAsyncEnumerable<AgentStreamEvent> ProcessRequestStreamAsync(
        string input,
        UserContext context,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var sessionId = await _sessionManager.StartSessionAsync(context);
        context.Preferences["sessionId"] = sessionId;

        await foreach (var streamEvent in _runtimeCoordinator.StreamAsync(
            sessionId,
            context,
            token => ProcessRequestCoreAsync(sessionId, input, context, token),
            ct))
        {
            yield return streamEvent;
        }
    }

    public async IAsyncEnumerable<AgentStreamEvent> ProcessDirectRequestStreamAsync(
        string input,
        UserContext context,
        string targetAgent,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var sessionId = await _sessionManager.StartSessionAsync(context);
        context.Preferences["sessionId"] = sessionId;

        await foreach (var streamEvent in _runtimeCoordinator.StreamAsync(
            sessionId,
            context,
            token => ProcessDirectRequestCoreAsync(sessionId, input, context, targetAgent, token),
            ct))
        {
            yield return streamEvent;
        }
    }

    private async Task<AgentResponse> ProcessRequestCoreAsync(string sessionId, string input, UserContext context, CancellationToken ct)
    {
        using var llmScope = _llmRuntimeContextAccessor.BeginScope(context, sessionId);

        try
        {
            _logger.LogInformation("🎯 Workflow executando request: {Input}", input[..Math.Min(50, input.Length)]);
            _logger.LogDebug("Delegating to Framework Orchestrator");
            return await _frameworkOrchestrator.ExecuteAsync(sessionId, input, context, ct);
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

            return AgentResponse.Error("Erro interno ao processar a requisição.", "MetaAgentOrchestrator");
        }
    }
    
    public async Task<IEnumerable<AgentInfo>> GetActiveAgentsAsync()
    {
        return await _agentFactory.GetAllAgentsAsync();
    }
    
    public async Task CleanupInactiveAgentsAsync()
    {
        var inactiveThreshold = TimeSpan.FromHours(24);
        var cutoff = DateTime.UtcNow - inactiveThreshold;
        var totalCleaned = 0;

        foreach (var tier in new[] { AgentTier.Support, AgentTier.Specialist })
        {
            var agents = await _agentFactory.GetAgentsByTierAsync(tier);
            foreach (var agent in agents)
            {
                if (agent.LastUsedAt < cutoff)
                {
                    await _agentFactory.RemoveAgentAsync(agent.Name);
                    _logger.LogInformation("🧹 Agent removido por inatividade: {Agent} (Tier {Tier}, LastUsed: {LastUsed})",
                        agent.Name, agent.Tier, agent.LastUsedAt);
                    totalCleaned++;
                }
            }
        }

        _logger.LogInformation("🧹 Cleanup concluído: {Count} agents inativos removidos", totalCleaned);
    }

    public async Task<AgentResponse> ProcessDirectRequestAsync(string input, UserContext context, string targetAgent)
    {
        var sessionId = await _sessionManager.StartSessionAsync(context);
        context.Preferences["sessionId"] = sessionId;
        using var scope = _runtimeCoordinator.BeginExecutionScope(sessionId, context);
        return await ProcessDirectRequestCoreAsync(sessionId, input, context, targetAgent, CancellationToken.None);
    }

    private async Task<AgentResponse> ProcessDirectRequestCoreAsync(string sessionId, string input, UserContext context, string targetAgent, CancellationToken ct)
    {
        using var llmScope = _llmRuntimeContextAccessor.BeginScope(context, sessionId);
        return await _directAgentRequestExecutor.ExecuteAsync(sessionId, input, context, targetAgent, ct);
    }
}