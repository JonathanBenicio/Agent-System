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
    private readonly IContextAnalyzer _contextAnalyzer;
    private readonly ISmartRouter _smartRouter;
    private readonly IAgentCollaborationWorkflow? _collaborationWorkflow;
    private readonly IWorkflowEngine? _workflowEngine;
    private readonly ITenantIsolationEnforcer? _isolationEnforcer;
    private readonly IEventPublisher? _eventPublisher;
    private readonly IWorkflowStore? _workflowStore;
    private readonly ILogger<MetaAgentOrchestrator> _logger;

    public MetaAgentOrchestrator(
        IFrameworkOrchestratorService frameworkOrchestrator,
        IDirectAgentRequestExecutor directAgentRequestExecutor,
        ILLMRuntimeContextAccessor llmRuntimeContextAccessor,
        IAgentFactory agentFactory,
        ISessionManager sessionManager,
        IAgentRuntimeCoordinator runtimeCoordinator,
        IContextAnalyzer contextAnalyzer,
        ISmartRouter smartRouter,
        ILogger<MetaAgentOrchestrator>? logger = null,
        IAgentCollaborationWorkflow? collaborationWorkflow = null,
        IWorkflowEngine? workflowEngine = null,
        ITenantIsolationEnforcer? isolationEnforcer = null,
        IEventPublisher? eventPublisher = null,
        IWorkflowStore? workflowStore = null)
    {
        _frameworkOrchestrator = frameworkOrchestrator;
        _directAgentRequestExecutor = directAgentRequestExecutor;
        _llmRuntimeContextAccessor = llmRuntimeContextAccessor;
        _agentFactory = agentFactory;
        _sessionManager = sessionManager;
        _runtimeCoordinator = runtimeCoordinator;
        _contextAnalyzer = contextAnalyzer ?? new NullContextAnalyzer();
        _smartRouter = smartRouter;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<MetaAgentOrchestrator>.Instance;
        _collaborationWorkflow = collaborationWorkflow;
        _workflowEngine = workflowEngine;
        _isolationEnforcer = isolationEnforcer;
        _eventPublisher = eventPublisher;
        _workflowStore = workflowStore;
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
            sessionManager,
            runtimeCoordinator,
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
        try
        {
            var response = await ProcessRequestCoreAsync(sessionId, input, context, CancellationToken.None);
            await _sessionManager.EndSessionAsync(sessionId);
            return response;
        }
        catch (Exception)
        {
            // Session ending on exception is already handled in ProcessRequestCoreAsync
            throw;
        }
    }

    public async IAsyncEnumerable<AgentStreamEvent> ProcessRequestStreamAsync(
        string input,
        UserContext context,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var sessionId = await _sessionManager.StartSessionAsync(context);
        context.Preferences["sessionId"] = sessionId;

        if (_eventPublisher != null)
        {
            await _eventPublisher.PublishAsync(new SessionCreatedEvent(sessionId, context.UserId, context.TenantId), ct);
        }

        try
        {
            await foreach (var streamEvent in _runtimeCoordinator.StreamAsync(
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
            await _sessionManager.EndSessionAsync(sessionId);

            if (_eventPublisher != null)
            {
                await _eventPublisher.PublishAsync(new SessionEndedEvent(sessionId, context.UserId, context.TenantId), ct);
            }
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

        if (_eventPublisher != null)
        {
            await _eventPublisher.PublishAsync(new SessionCreatedEvent(sessionId, context.UserId, context.TenantId), ct);
        }

        try
        {
            await foreach (var streamEvent in _runtimeCoordinator.StreamAsync(
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
            await _sessionManager.EndSessionAsync(sessionId);

            if (_eventPublisher != null)
            {
                await _eventPublisher.PublishAsync(new SessionEndedEvent(sessionId, context.UserId, context.TenantId), ct);
            }
        }
    }

    private async Task<AgentResponse> ProcessRequestCoreAsync(string sessionId, string input, UserContext context, CancellationToken ct)
    {
        using var llmScope = _llmRuntimeContextAccessor.BeginScope(context, sessionId);

        var effectiveInput = await InjectMemoryContextAsync(input, context, ct);

        try
        {
            // Intercept conversational workflow commands via chat
            if (_workflowEngine != null && _workflowStore != null)
            {
                var inputTrimmed = input.Trim();
                var inputLower = inputTrimmed.ToLowerInvariant();
                var tenantId = string.IsNullOrWhiteSpace(context.TenantId) ? Tenant.DefaultTenantId : context.TenantId;

                // 1. Start Workflow
                if (inputLower.StartsWith("iniciar workflow ") || inputLower.StartsWith("executar workflow ") || inputLower.StartsWith("rodar workflow "))
                {
                    var workflowId = inputTrimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries).Last();
                    var definition = await _workflowStore.GetDefinitionAsync(tenantId, workflowId, ct);
                    if (definition == null)
                    {
                        return AgentResponse.Error($"❌ Workflow com ID '{workflowId}' não encontrado para o tenant atual.", "WorkflowEngine");
                    }

                    var execution = await _workflowEngine.StartAsync(tenantId, definition, initiatedBy: context.UserId, ct: ct);
                    return AgentResponse.Ok(
                        $"🚀 Workflow **{definition.Name}** iniciado com sucesso!\n\n" +
                        $"* **ID da Execução:** `{execution.Id}`\n" +
                        $"* **Status:** `{execution.Status}`\n" +
                        $"* **Iniciado por:** `{execution.InitiatedBy}`\n\n" +
                        $"O progresso detalhado de cada etapa está sendo transmitido em tempo real pelo SignalR.",
                        "WorkflowEngine",
                        AgentTier.Support);
                }

                // 2. Cancel Workflow
                if (inputLower.StartsWith("cancelar workflow ") || inputLower.StartsWith("parar workflow ") || inputLower.StartsWith("abortar workflow "))
                {
                    var executionId = inputTrimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries).Last();
                    var execution = await _workflowEngine.GetExecutionAsync(tenantId, executionId, ct);
                    if (execution == null)
                    {
                        return AgentResponse.Error($"❌ Execução de workflow com ID '{executionId}' não encontrada.", "WorkflowEngine");
                    }

                    var cancelled = await _workflowEngine.CancelAsync(tenantId, executionId, "Cancelado via chat conversacional pelo usuário.", ct);
                    return AgentResponse.Ok(
                        $"⏹️ Workflow **{cancelled.WorkflowName}** (Execução `{cancelled.Id}`) foi cancelado com sucesso!\n\n" +
                        $"* **Status Atual:** `{cancelled.Status}`\n" +
                        $"* **Motivo:** {cancelled.ErrorMessage}\n" +
                        $"* **Encerrado em:** {cancelled.CompletedAt?.ToString("g")}",
                        "WorkflowEngine",
                        AgentTier.Support);
                }

                // 3. List Workflows
                if (inputLower == "listar workflows" || inputLower == "status dos workflows" || inputLower == "workflows ativos")
                {
                    var executions = await _workflowEngine.ListExecutionsAsync(tenantId, limit: 10, ct: ct);
                    if (executions.Count == 0)
                    {
                        return AgentResponse.Ok("📋 Nenhum workflow recente foi executado neste tenant.", "WorkflowEngine", AgentTier.Support);
                    }

                    var sb = new System.Text.StringBuilder();
                    sb.AppendLine("📋 **Histórico Recente de Execuções de Workflows:**\n");
                    foreach (var exec in executions)
                    {
                        var duration = exec.CompletedAt.HasValue ? $" (Duração: {(exec.CompletedAt.Value - exec.StartedAt).TotalSeconds:F1}s)" : "";
                        sb.AppendLine($"* **ID:** `{exec.Id}` | **Workflow:** {exec.WorkflowName} | **Status:** `{exec.Status}`{duration}");
                    }

                    return AgentResponse.Ok(sb.ToString(), "WorkflowEngine", AgentTier.Support);
                }
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

            // 2. Delegate to Workflow Engine if specific complex intent or workflow template is matched
            if (_workflowEngine != null && (analysis.Intent == IntentType.Analyze || analysis.Intent == IntentType.Plan))
            {
                _logger.LogInformation("Delegating to Workflow Engine for intent: {Intent}", analysis.Intent);
                // In a real implementation, we'd resolve the workflow definition first
                // For this deep integration, we signal the intent
                context.Preferences["workflow.intent"] = analysis.Intent.ToString();
            }

            // 3. Delegate to Collaboration Workflow if debate or high complexity (Phase 2 Integration)
            if (_collaborationWorkflow != null && await _collaborationWorkflow.ShouldRunAsync(effectiveInput, analysis, ct))
            {
                _logger.LogInformation("Delegating to Collaboration Workflow (Multi-Agent)");
                return await _collaborationWorkflow.ExecuteAsync(sessionId, effectiveInput, context, analysis, ct);
            }

            // 4. Fallback to Primary Framework Orchestrator
            _logger.LogDebug("Delegating to Framework Orchestrator");
            return await _frameworkOrchestrator.ExecuteAsync(sessionId, effectiveInput, context, ct);
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
        try
        {
            var response = await ProcessDirectRequestCoreAsync(sessionId, input, context, targetAgent, CancellationToken.None);
            await _sessionManager.EndSessionAsync(sessionId);
            return response;
        }
        catch (Exception)
        {
            // In a real scenario, we might want to end session here too if not handled in core
            await _sessionManager.EndSessionAsync(sessionId);
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