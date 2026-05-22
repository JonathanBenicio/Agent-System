using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Services;

/// <summary>
/// Meta-Agent principal: analisa contexto, roteia para agents e gerencia sessões.
/// Inspirado no Tech Lead do Labs com capacidade de orquestração.
/// Refactored: dependencies reduced from 14 to 7 via SessionLifecycleCoordinator and ChatWorkflowCommandHandler.
/// </summary>
public class MetaAgentOrchestrator : IMetaAgent
{
    private readonly IFrameworkOrchestratorService _frameworkOrchestrator;
    private readonly IDirectAgentRequestExecutor _directAgentRequestExecutor;
    private readonly ILLMRuntimeContextAccessor _llmRuntimeContextAccessor;
    private readonly IAgentFactory _agentFactory;
    private readonly ISessionLifecycleCoordinator _sessionCoordinator;
    private readonly ISessionManager _sessionManager;
    private readonly IContextAnalyzer _contextAnalyzer;
    private readonly ISmartRouter _smartRouter;
    private readonly IAgentCollaborationWorkflow? _collaborationWorkflow;
    private readonly IChatWorkflowCommandHandler? _chatWorkflowCommandHandler;
    private readonly ILogger<MetaAgentOrchestrator> _logger;

    public MetaAgentOrchestrator(
        IFrameworkOrchestratorService frameworkOrchestrator,
        IDirectAgentRequestExecutor directAgentRequestExecutor,
        ILLMRuntimeContextAccessor llmRuntimeContextAccessor,
        IAgentFactory agentFactory,
        ISessionLifecycleCoordinator sessionCoordinator,
        ISessionManager sessionManager,
        IContextAnalyzer contextAnalyzer,
        ISmartRouter smartRouter,
        ILogger<MetaAgentOrchestrator>? logger = null,
        IAgentCollaborationWorkflow? collaborationWorkflow = null,
        IChatWorkflowCommandHandler? chatWorkflowCommandHandler = null)
    {
        _frameworkOrchestrator = frameworkOrchestrator;
        _directAgentRequestExecutor = directAgentRequestExecutor;
        _llmRuntimeContextAccessor = llmRuntimeContextAccessor;
        _agentFactory = agentFactory;
        _sessionCoordinator = sessionCoordinator;
        _sessionManager = sessionManager;
        _contextAnalyzer = contextAnalyzer ?? new NullContextAnalyzer();
        _smartRouter = smartRouter;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<MetaAgentOrchestrator>.Instance;
        _collaborationWorkflow = collaborationWorkflow;
        _chatWorkflowCommandHandler = chatWorkflowCommandHandler;
    }

    // Backwards-compatible constructor for existing tests and custom setups
    public MetaAgentOrchestrator(
        IFrameworkOrchestratorService frameworkOrchestrator,
        IDirectAgentRequestExecutor directAgentRequestExecutor,
        ILLMRuntimeContextAccessor llmRuntimeContextAccessor,
        IAgentFactory agentFactory,
        ISessionManager sessionManager,
        IAgentRuntimeCoordinator runtimeCoordinator,
        ILogger<MetaAgentOrchestrator>? logger = null)
        : this(
            frameworkOrchestrator,
            directAgentRequestExecutor,
            llmRuntimeContextAccessor,
            agentFactory,
            new SessionLifecycleCoordinator(
                sessionManager,
                runtimeCoordinator,
                Microsoft.Extensions.Logging.Abstractions.NullLogger<SessionLifecycleCoordinator>.Instance),
            sessionManager,
            new NullContextAnalyzer(),
            new NullSmartRouter(),
            logger)
    {
    }

    private class NullSmartRouter : ISmartRouter
    {
        public Task<(bool IsFastPath, string? Response, AgenticSystem.Core.Models.Triage.QueryTriageResult? Triage)> TriageAsync(string input, UserContext context, CancellationToken ct = default) =>
            Task.FromResult((false, (string?)null, (AgenticSystem.Core.Models.Triage.QueryTriageResult?)null));

        public Task<RoutingDecision> RouteAsync(AnalysisResult analysis, UserContext context) =>
            Task.FromResult(new RoutingDecision());

        public Task<ProviderRoutingDecision> RouteProviderAsync(string? requestedProvider, string? requestedModel) =>
            Task.FromResult(new ProviderRoutingDecision());

        public Task RecordPerformanceAsync(string agentName, AgentPerformanceMetric metric) => Task.CompletedTask;

        public Task<IEnumerable<AgentRanking>> GetRankingsByDomainAsync(string domain) =>
            Task.FromResult(Enumerable.Empty<AgentRanking>());
    }

    private class NullContextAnalyzer : IContextAnalyzer
    {
        public Task<AnalysisResult> AnalyzeAsync(string input, UserContext userContext) =>
            Task.FromResult(new AnalysisResult { Intent = IntentType.Chat, Confidence = 1.0f });

        public Task<System.Collections.Generic.List<ExtractedEntity>> ExtractEntitiesAsync(string input) =>
            Task.FromResult(new System.Collections.Generic.List<ExtractedEntity>());

        public Task<bool> RequiresDelegationAsync(AnalysisResult analysis) =>
            Task.FromResult(false);
    }

    public async Task<AgentResponse> ProcessRequestAsync(string input, UserContext context)
    {
        if (!string.IsNullOrEmpty(context.TenantId) && !await _sessionCoordinator.CanStartSessionAsync(context.TenantId))
        {
            return AgentResponse.Error("🚫 Limite de sessões simultâneas atingido para o seu tenant.");
        }

        var sessionId = await _sessionCoordinator.StartSessionAsync(context);
        using var scope = _sessionCoordinator.BeginExecutionScope(sessionId, context);
        try
        {
            var response = await ProcessRequestCoreAsync(sessionId, input, context, CancellationToken.None);
            await _sessionCoordinator.EndSessionAsync(sessionId, context);
            return response;
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async IAsyncEnumerable<AgentStreamEvent> ProcessRequestStreamAsync(
        string input,
        UserContext context,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var sessionId = await _sessionCoordinator.StartSessionAsync(context, ct);

        try
        {
            await foreach (var streamEvent in _sessionCoordinator.StreamAsync(
                sessionId,
                context,
                token => ProcessRequestCoreAsync(sessionId, input, context, token),
                ct))
            {
                yield return streamEvent;
            }
        }
        finally
        {
            await _sessionCoordinator.EndSessionAsync(sessionId, context, ct);
        }
    }

    public async IAsyncEnumerable<AgentStreamEvent> ProcessDirectRequestStreamAsync(
        string input,
        UserContext context,
        string targetAgent,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var sessionId = await _sessionCoordinator.StartSessionAsync(context, ct);

        try
        {
            await foreach (var streamEvent in _sessionCoordinator.StreamAsync(
                sessionId,
                context,
                token => ProcessDirectRequestCoreAsync(sessionId, input, context, targetAgent, token),
                ct))
            {
                yield return streamEvent;
            }
        }
        finally
        {
            await _sessionCoordinator.EndSessionAsync(sessionId, context, ct);
        }
    }

    private async Task<AgentResponse> ProcessRequestCoreAsync(string sessionId, string input, UserContext context, CancellationToken ct)
    {
        using var llmScope = _llmRuntimeContextAccessor.BeginScope(context, sessionId);

        var effectiveInput = await InjectMemoryContextAsync(input, context, ct);

        try
        {
            // Intercept conversational workflow commands via chat
            if (_chatWorkflowCommandHandler != null)
            {
                var workflowResponse = await _chatWorkflowCommandHandler.TryHandleAsync(effectiveInput, context, ct);
                if (workflowResponse != null)
                    return workflowResponse;
            }

            // 0. Triage Layer (Fast Path & Complexity Analysis)
            var (isFastPath, fastPathResponse, triage) = await _smartRouter.TriageAsync(effectiveInput, context, ct);
            if (isFastPath)
            {
                return AgentResponse.Ok(fastPathResponse ?? string.Empty, "FastPath", AgentTier.Support);
            }

            if (triage != null)
            {
                context.Preferences["triage.intent"] = triage.Intent.ToString();
                context.Preferences["triage.complexity"] = triage.Complexity.ToString();
                context.Preferences["triage.requiresRag"] = triage.RequiresRAG.ToString();
                context.Preferences["triage.requiresTools"] = triage.RequiresTools.ToString();
                context.Preferences["triage.recommendedTier"] = triage.RecommendedAgentTier;

                // Tiered Execution (Camada 2): Switch based on ComplexityLevel
                if (triage.Complexity == AgenticSystem.Core.Models.Triage.ComplexityLevel.Low && 
                    triage.Intent == AgenticSystem.Core.Models.Triage.IntentType.DirectAnswer)
                {
                    _logger.LogInformation("⚡ Executing Tier 1 logic (Low Complexity Direct Execution) for input: {Input}", effectiveInput[..Math.Min(50, effectiveInput.Length)]);
                    var target = !string.IsNullOrWhiteSpace(triage.EstimatedAgent) ? triage.EstimatedAgent : "GeneralAgent";
                    return await _directAgentRequestExecutor.ExecuteAsync(sessionId, effectiveInput, context, target, ct);
                }

            }

            _logger.LogInformation("🎯 Workflow executando request: {Input}", effectiveInput[..Math.Min(50, effectiveInput.Length)]);

            // 1. Analyze Context for Routing Decisions (Phase 2 Integration)
            var analysis = await _contextAnalyzer.AnalyzeAsync(effectiveInput, context);

            // 2. Delegate to Collaboration Workflow if debate or high complexity (Phase 2 Integration)
            if (_collaborationWorkflow != null && await _collaborationWorkflow.ShouldRunAsync(effectiveInput, analysis, ct))
            {
                _logger.LogInformation("Delegating to Collaboration Workflow (Multi-Agent)");
                return await _collaborationWorkflow.ExecuteAsync(sessionId, effectiveInput, context, analysis, ct);
            }

            // 3. Fallback to Primary Framework Orchestrator
            _logger.LogDebug("Delegating to Framework Orchestrator");
            return await _frameworkOrchestrator.ExecuteAsync(sessionId, effectiveInput, context, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Workflow execution failed: {Message}", ex.Message);
            await _sessionCoordinator.PublishEventAsync(new AgentStreamEvent
            {
                Type = AgentStreamEventType.Error,
                Message = ex.Message,
                Data = new Dictionary<string, object>
                {
                    ["fallback"] = false,
                    ["workflow"] = "primary"
                }
            }, ct);

            try { await _sessionCoordinator.EndSessionAsync(sessionId, context, ct); }
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
        var sessionId = await _sessionCoordinator.StartSessionAsync(context);
        using var scope = _sessionCoordinator.BeginExecutionScope(sessionId, context);
        try
        {
            var response = await ProcessDirectRequestCoreAsync(sessionId, input, context, targetAgent, CancellationToken.None);
            await _sessionCoordinator.EndSessionAsync(sessionId, context);
            return response;
        }
        catch (Exception)
        {
            await _sessionCoordinator.EndSessionAsync(sessionId, context);
            throw;
        }
    }

    private async Task<AgentResponse> ProcessDirectRequestCoreAsync(string sessionId, string input, UserContext context, string targetAgent, CancellationToken ct)
    {
        using var llmScope = _llmRuntimeContextAccessor.BeginScope(context, sessionId);
        var effectiveInput = await InjectMemoryContextAsync(input, context, ct);
        return await _directAgentRequestExecutor.ExecuteAsync(sessionId, effectiveInput, context, targetAgent, ct);
    }

    private async Task<string> InjectMemoryContextAsync(string input, UserContext context, CancellationToken ct)
    {
        var tenantId = string.IsNullOrWhiteSpace(context.TenantId) ? Tenant.DefaultTenantId : context.TenantId;
        var memoryContext = await _sessionManager.GetMemoryContextAsync(input, context.UserId, tenantId, ct);

        if (string.IsNullOrEmpty(memoryContext))
        {
            return input;
        }

        // Flag that memory was used for this request
        context.Preferences["memory_injected"] = true;
        
        _logger.LogDebug("🧠 Injected {Length} chars of memory context", memoryContext.Length);
        return memoryContext + "\n\n" + input;
    }
}