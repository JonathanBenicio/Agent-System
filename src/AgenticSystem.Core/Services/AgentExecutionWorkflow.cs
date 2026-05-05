using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Core.Services;

public class AgentExecutionWorkflow : IAgentExecutionWorkflow
{
    private readonly IContextAnalyzer _contextAnalyzer;
    private readonly IAgentFactory _agentFactory;
    private readonly ISessionManager _sessionManager;
    private readonly IDynamicAgentService _dynamicAgentService;
    private readonly IHandoffManager _handoffManager;
    private readonly IToolAvailabilityGuard _toolGuard;
    private readonly IConfidenceScoreCalculator _confidenceCalculator;
    private readonly IAgentRuntimeCoordinator _runtimeCoordinator;
    private readonly ILLMRuntimeContextAccessor _llmRuntimeContextAccessor;
    private readonly IAgentCollaborationWorkflow? _collaborationWorkflow;
    private readonly IFinalResponseApprovalService? _finalApprovalService;
    private readonly IRAGService? _ragService;
    private readonly IContextBudgetManager? _contextBudgetManager;
    private readonly ISmartRouter? _smartRouter;
    private readonly IQualityGateService? _qualityGateService;
    private readonly IReflectionEngine? _reflectionEngine;
    private readonly ICorrectionLoop? _correctionLoop;
    private readonly IAgentMemoryService? _agentMemoryService;
    private readonly IFrameworkOrchestratorService? _frameworkOrchestrator;
    private readonly ILogger<AgentExecutionWorkflow> _logger;

    public AgentExecutionWorkflow(
        IContextAnalyzer contextAnalyzer,
        IAgentFactory agentFactory,
        ISessionManager sessionManager,
        IDynamicAgentService dynamicAgentService,
        IHandoffManager handoffManager,
        IToolAvailabilityGuard toolGuard,
        IConfidenceScoreCalculator confidenceCalculator,
        IAgentRuntimeCoordinator runtimeCoordinator,
        ILLMRuntimeContextAccessor llmRuntimeContextAccessor,
        ILogger<AgentExecutionWorkflow> logger,
        IAgentCollaborationWorkflow? collaborationWorkflow = null,
        IFinalResponseApprovalService? finalApprovalService = null,
        IRAGService? ragService = null,
        IContextBudgetManager? contextBudgetManager = null,
        ISmartRouter? smartRouter = null,
        IQualityGateService? qualityGateService = null,
        IReflectionEngine? reflectionEngine = null,
        ICorrectionLoop? correctionLoop = null,
        IAgentMemoryService? agentMemoryService = null,
        IFrameworkOrchestratorService? frameworkOrchestrator = null)
    {
        _contextAnalyzer = contextAnalyzer;
        _agentFactory = agentFactory;
        _sessionManager = sessionManager;
        _dynamicAgentService = dynamicAgentService;
        _handoffManager = handoffManager;
        _toolGuard = toolGuard;
        _confidenceCalculator = confidenceCalculator;
        _runtimeCoordinator = runtimeCoordinator;
        _llmRuntimeContextAccessor = llmRuntimeContextAccessor;
        _logger = logger;
        _collaborationWorkflow = collaborationWorkflow;
        _finalApprovalService = finalApprovalService;
        _ragService = ragService;
        _contextBudgetManager = contextBudgetManager;
        _smartRouter = smartRouter;
        _qualityGateService = qualityGateService;
        _reflectionEngine = reflectionEngine;
        _correctionLoop = correctionLoop;
        _agentMemoryService = agentMemoryService;
        _frameworkOrchestrator = frameworkOrchestrator;
    }

    public async Task<AgentResponse> ExecuteAsync(string sessionId, string input, UserContext context, CancellationToken ct = default)
    {
        using var llmScope = _llmRuntimeContextAccessor.BeginScope(context, sessionId);

        try
        {
            _logger.LogInformation("🎯 Workflow executando request: {Input}", input[..Math.Min(50, input.Length)]);

            // Delegar ao Framework Orchestrator quando disponível
            if (_frameworkOrchestrator is not null)
            {
                _logger.LogDebug("Delegating to Framework Orchestrator");
                var orchestratorResponse = await _frameworkOrchestrator.ExecuteAsync(sessionId, input, context, ct);
                return orchestratorResponse;
            }

            // Fallback: fluxo legado completo
            var analysis = await _contextAnalyzer.AnalyzeAsync(input, context);
            await ValidatePreExecutionAsync(input, analysis, ct);

            var toolCheck = await _toolGuard.CheckAsync(analysis.RequiredTools, ct);
            if (toolCheck.NoneAvailable)
            {
                return BuildUnavailableToolsResponse(sessionId, toolCheck);
            }

            if (await _dynamicAgentService.IsAgentCreationRequestAsync(input, analysis))
            {
                using var dynamicScope = _runtimeCoordinator.BeginAgentScope("DynamicAgentService");
                var created = await _dynamicAgentService.HandleAgentCreationAsync(input, context);
                created.SessionId = sessionId;
                return created;
            }

            if (_smartRouter != null)
            {
                var routingDecision = await _smartRouter.RouteAsync(analysis, context);
                if (!string.IsNullOrWhiteSpace(routingDecision.PrimaryAgent))
                {
                    analysis.EstimatedAgent = routingDecision.PrimaryAgent;
                }
            }

            AgentResponse response;
            RAGContext? ragContext = null;
            IEnumerable<Reflection>? reflections = null;
            Reflection? latestReflection = null;
            AgentInfo? selectedAgentInfo = null;
            var executionSw = System.Diagnostics.Stopwatch.StartNew();

            if (_collaborationWorkflow != null && await _collaborationWorkflow.ShouldRunAsync(input, analysis, ct))
            {
                response = await _collaborationWorkflow.ExecuteAsync(sessionId, input, context, analysis, ct);
                response.Metadata["executionMode"] = "planner-executor-reviewer";
            }
            else
            {
                var agent = await _agentFactory.GetOrCreateAgentAsync(analysis);
                selectedAgentInfo = new AgentInfo
                {
                    Name = agent.Name,
                    Domain = agent.Domain,
                    Tier = agent.Tier,
                    AvailableTools = agent.AvailableTools.ToList()
                };

                await _runtimeCoordinator.PublishEventAsync(new AgentStreamEvent
                {
                    Type = AgentStreamEventType.AgentSelected,
                    AgentName = agent.Name,
                    Message = agent.Description,
                    Data = new Dictionary<string, object>
                    {
                        ["tier"] = agent.Tier.ToString(),
                        ["domain"] = agent.Domain
                    }
                }, ct);

                var enrichedInput = input;
                ragContext = await RetrieveRAGContextAsync(input, analysis, agent, ct);
                if (ragContext != null && !string.IsNullOrWhiteSpace(ragContext.BuiltContext))
                {
                    enrichedInput = $"[Contexto Relevante]\n{ragContext.BuiltContext}\n\n[Pergunta do Usuário]\n{input}";
                }

                if (_correctionLoop != null)
                {
                    var rules = (await _correctionLoop.GetActiveRulesAsync(context.UserId, agent.Name)).ToList();
                    if (rules.Count > 0)
                    {
                        enrichedInput = await _correctionLoop.ApplyRulesToPromptAsync(enrichedInput, rules);
                    }
                }

                var handoffDecision = await _handoffManager.EvaluateHandoffAsync(analysis, agent);
                if (handoffDecision.ShouldHandoff)
                {
                    response = await _handoffManager.ExecuteHandoffAsync(enrichedInput, context, handoffDecision);
                    await _handoffManager.RecordHandoffAsync(sessionId, new HandoffRecord
                    {
                        SessionId = sessionId,
                        SourceAgent = agent.Name,
                        TargetAgent = string.Join(",", handoffDecision.Targets.Select(target => target.AgentName)),
                        Reason = handoffDecision.Reason,
                        Strategy = handoffDecision.Strategy,
                        Success = response.Success
                    });
                }
                else
                {
                    using var agentScope = _runtimeCoordinator.BeginAgentScope(agent.Name, agent.AvailableTools);
                    response = await agent.ExecuteAsync(enrichedInput, context);
                }

                if (_reflectionEngine != null)
                {
                    latestReflection = await _reflectionEngine.ReflectAsync(
                        sessionId,
                        agent.Name,
                        input,
                        response.Content ?? string.Empty,
                        response.Success ? 0.85 : 0.25);
                    reflections = await _reflectionEngine.GetSessionReflectionsAsync(sessionId);

                    if (_correctionLoop != null && !string.IsNullOrWhiteSpace(latestReflection.ImprovementSuggestion))
                    {
                        await _correctionLoop.AddRuleAsync(
                            context.UserId,
                            latestReflection.ImprovementSuggestion,
                            scope: "auto-reflection",
                            targetAgent: agent.Name);
                    }
                }

                response.Metadata["executionMode"] = "single-agent";
            }

            executionSw.Stop();
            response.SessionId = sessionId;

            await ValidatePostExecutionAsync(input, response, ct);
            response.Confidence = _confidenceCalculator.Calculate(response, ragContext: ragContext, reflections: reflections, toolAvailability: toolCheck);

            if (_smartRouter != null && selectedAgentInfo is not null)
            {
                await _smartRouter.RecordPerformanceAsync(selectedAgentInfo.Name, new AgentPerformanceMetric
                {
                    Domain = selectedAgentInfo.Domain,
                    Latency = executionSw.Elapsed,
                    Success = response.Success,
                    UserSatisfaction = null
                });
            }

            var approvalResponse = await ApplyFinalApprovalAsync(sessionId, input, analysis, response, ct);
            if (approvalResponse is not null)
            {
                return approvalResponse;
            }

            await PersistExecutionResultAsync(sessionId, input, context, analysis, response, executionSw.Elapsed, ct);

            if (_agentMemoryService != null)
            {
                var memoryAgentName = selectedAgentInfo?.Name ?? response.AgentName;
                if (!string.IsNullOrWhiteSpace(memoryAgentName))
                {
                    await _agentMemoryService.RecordInteractionAsync(
                        sessionId,
                        memoryAgentName,
                        context,
                        input,
                        response,
                        latestReflection,
                        ct);
                }
            }

            return response;
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

        try
        {
            var agents = await _agentFactory.GetAllAgentsAsync();
            var requestedAgent = agents.FirstOrDefault(agent => agent.Name.Equals(targetAgent, StringComparison.OrdinalIgnoreCase));
            if (requestedAgent is null)
            {
                return AgentResponse.Error($"Agent '{targetAgent}' não encontrado.", "AgentExecutionWorkflow");
            }

            var analysis = new AnalysisResult
            {
                PrimaryDomain = requestedAgent.Domain,
                Intent = IntentType.Chat,
                RecommendedTier = requestedAgent.Tier,
                EstimatedAgent = requestedAgent.Name,
                RequiredTools = new List<string>(requestedAgent.AvailableTools),
                Confidence = 1
            };

            await ValidatePreExecutionAsync(input, analysis, ct);

            var executableAgent = await _agentFactory.GetOrCreateAgentAsync(analysis);
            var enrichedInput = input;
            if (_correctionLoop != null)
            {
                var rules = (await _correctionLoop.GetActiveRulesAsync(context.UserId, executableAgent.Name)).ToList();
                if (rules.Count > 0)
                {
                    enrichedInput = await _correctionLoop.ApplyRulesToPromptAsync(enrichedInput, rules);
                }
            }

            await _runtimeCoordinator.PublishEventAsync(new AgentStreamEvent
            {
                Type = AgentStreamEventType.AgentSelected,
                AgentName = executableAgent.Name,
                Message = executableAgent.Description,
                Data = new Dictionary<string, object>
                {
                    ["directRequest"] = true,
                    ["targetAgent"] = targetAgent
                }
            }, ct);

            var executionSw = System.Diagnostics.Stopwatch.StartNew();
            using var agentScope = _runtimeCoordinator.BeginAgentScope(executableAgent.Name, executableAgent.AvailableTools);
            var response = await executableAgent.ExecuteAsync(enrichedInput, context);
            executionSw.Stop();

            response.SessionId = sessionId;
            response.Metadata["executionMode"] = "direct";

            await ValidatePostExecutionAsync(input, response, ct);

            IEnumerable<Reflection>? reflections = null;
            Reflection? latestReflection = null;
            if (_reflectionEngine != null)
            {
                latestReflection = await _reflectionEngine.ReflectAsync(
                    sessionId,
                    executableAgent.Name,
                    input,
                    response.Content ?? string.Empty,
                    response.Success ? 0.85 : 0.25);
                reflections = await _reflectionEngine.GetSessionReflectionsAsync(sessionId);

                if (_correctionLoop != null && !string.IsNullOrWhiteSpace(latestReflection.ImprovementSuggestion))
                {
                    await _correctionLoop.AddRuleAsync(
                        context.UserId,
                        latestReflection.ImprovementSuggestion,
                        scope: "auto-reflection",
                        targetAgent: executableAgent.Name);
                }
            }

            response.Confidence = _confidenceCalculator.Calculate(response, ragContext: null, reflections: reflections, toolAvailability: null);

            var approvalResponse = await ApplyFinalApprovalAsync(sessionId, input, analysis, response, ct);
            if (approvalResponse is not null)
            {
                return approvalResponse;
            }

            await PersistExecutionResultAsync(sessionId, input, context, analysis, response, executionSw.Elapsed, ct, directRequest: true, targetAgent: targetAgent);

            if (_agentMemoryService != null)
            {
                await _agentMemoryService.RecordInteractionAsync(
                    sessionId,
                    executableAgent.Name,
                    context,
                    input,
                    response,
                    latestReflection,
                    ct);
            }

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Direct workflow failed for {Agent}", targetAgent);
            await _runtimeCoordinator.PublishEventAsync(new AgentStreamEvent
            {
                Type = AgentStreamEventType.Error,
                Message = ex.Message,
                Data = new Dictionary<string, object>
                {
                    ["directRequest"] = true,
                    ["targetAgent"] = targetAgent
                }
            }, ct);

            try { await _sessionManager.EndSessionAsync(sessionId); }
            catch (Exception endEx) { _logger.LogWarning(endEx, "Falha ao finalizar sessão {SessionId}", sessionId); }

            return AgentResponse.Error("Erro interno ao processar requisição direta.", "AgentExecutionWorkflow");
        }
    }

    private async Task ValidatePreExecutionAsync(string input, AnalysisResult analysis, CancellationToken ct)
    {
        if (_qualityGateService != null)
        {
            var qualityReport = await _qualityGateService.ValidateRequestAsync(input, ct: ct);
            if (!qualityReport.OverallPassed)
            {
                var issues = string.Join("; ", qualityReport.Results.Where(result => !result.Passed).SelectMany(result => result.Issues));
                throw new InvalidOperationException(issues);
            }
            return;
        }

        if (string.IsNullOrWhiteSpace(input))
        {
            throw new InvalidOperationException("Input não pode estar vazio.");
        }

        if (input.Length > 10_000)
        {
            throw new InvalidOperationException("Input muito longo (máximo 10.000 caracteres).");
        }

        if (analysis.Confidence < 0.3)
        {
            throw new InvalidOperationException("Não foi possível entender a solicitação. Tente ser mais específico.");
        }
    }

    private async Task ValidatePostExecutionAsync(string input, AgentResponse response, CancellationToken ct)
    {
        if (_qualityGateService != null)
        {
            await _qualityGateService.ValidateResponseAsync(input, response.Content ?? string.Empty, ct: ct);
        }
    }

    private AgentResponse BuildUnavailableToolsResponse(string sessionId, ToolAvailabilityResult toolCheck)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("❌ Não é possível executar esta solicitação — ferramentas necessárias não estão disponíveis.");
        sb.AppendLine();
        sb.AppendLine($"Tools ausentes: {string.Join(", ", toolCheck.MissingTools)}");

        if (toolCheck.Suggestions.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Sugestões de extensões/MCPs:");
            foreach (var suggestion in toolCheck.Suggestions.Where(suggestion => suggestion.RelevanceScore >= 0.5))
            {
                sb.AppendLine($"- {suggestion.PackageName} ({suggestion.Source}) — {suggestion.Description}");
                sb.AppendLine($"  Instalar: {suggestion.InstallCommand}");
            }
        }

        return new AgentResponse
        {
            Success = false,
            Content = sb.ToString(),
            AgentName = "AgentExecutionWorkflow",
            SessionId = sessionId,
            Metadata = new Dictionary<string, object>
            {
                ["toolAvailability"] = toolCheck,
                ["suggestions"] = toolCheck.Suggestions
            }
        };
    }

    private async Task<RAGContext?> RetrieveRAGContextAsync(string input, AnalysisResult analysis, IAgent agent, CancellationToken ct)
    {
        if (_ragService is null)
        {
            return null;
        }

        try
        {
            var ragContext = await _ragService.RetrieveContextAsync(new RAGQuery
            {
                Query = input,
                AgentId = agent.Name,
                Scope = SearchScope.All,
                MaxResults = 10,
                TopKAfterReRank = 5,
                MinRelevanceScore = 0.3
            }, ct);

            if (_contextBudgetManager != null)
            {
                var budget = _contextBudgetManager.ResolveBudget(analysis);
                ragContext = await _contextBudgetManager.TrimContextToBudgetAsync(ragContext, budget);
            }

            return ragContext;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "RAG context retrieval failed, proceeding without context");
            return null;
        }
    }

    private async Task<AgentResponse?> ApplyFinalApprovalAsync(
        string sessionId,
        string input,
        AnalysisResult analysis,
        AgentResponse response,
        CancellationToken ct)
    {
        if (_finalApprovalService is null)
        {
            return null;
        }

        var decision = await _finalApprovalService.EvaluateAsync(sessionId, input, analysis, response, ct);
        if (decision.Allowed || !decision.RequiresApproval || decision.ApprovalRequest is null)
        {
            return null;
        }

        var pendingResponse = new AgentResponse
        {
            Success = false,
            AgentName = response.AgentName,
            AgentTier = response.AgentTier,
            SessionId = sessionId,
            Content = "Resposta gerada e aguardando aprovação humana antes da publicação final.",
            Metadata = new Dictionary<string, object>(response.Metadata)
            {
                ["pendingFinalApproval"] = true,
                ["finalApprovalId"] = decision.ApprovalRequest.Id,
                ["finalApprovalReason"] = decision.Reason,
                ["proposedResponse"] = response.Content,
                ["approvalKind"] = "final-response"
            },
            ActionsPerformed = response.ActionsPerformed.ToList(),
            ToolsUsed = response.ToolsUsed.ToList(),
            Confidence = response.Confidence
        };

        await _sessionManager.AddEventAsync(sessionId, new AgentEvent
        {
            SessionId = sessionId,
            AgentName = response.AgentName,
            AgentTier = response.AgentTier,
            UserInput = input,
            AgentResponse = pendingResponse.Content,
            ActionsPerformed = pendingResponse.ActionsPerformed,
            ToolsUsed = pendingResponse.ToolsUsed,
            Context = new Dictionary<string, object>
            {
                ["pendingFinalApproval"] = true,
                ["finalApprovalId"] = decision.ApprovalRequest.Id,
                ["finalApprovalReason"] = decision.Reason
            }
        });

        return pendingResponse;
    }

    private async Task PersistExecutionResultAsync(
        string sessionId,
        string input,
        UserContext context,
        AnalysisResult analysis,
        AgentResponse response,
        TimeSpan latency,
        CancellationToken ct,
        bool directRequest = false,
        string? targetAgent = null)
    {
        await _sessionManager.AddEventAsync(sessionId, new AgentEvent
        {
            SessionId = sessionId,
            AgentName = response.AgentName,
            AgentTier = response.AgentTier,
            UserInput = input,
            AgentResponse = response.Content ?? string.Empty,
            ActionsPerformed = response.ActionsPerformed,
            ToolsUsed = response.ToolsUsed,
            Context = new Dictionary<string, object>
            {
                ["analysis"] = analysis,
                ["user_context"] = context,
                ["executionMode"] = response.Metadata.TryGetValue("executionMode", out var mode) ? mode?.ToString() ?? "single-agent" : "single-agent",
                ["directRequest"] = directRequest,
                ["targetAgent"] = targetAgent ?? string.Empty
            }
        });

        await _runtimeCoordinator.RecordArtifactAsync(new AgentExecutionArtifact
        {
            SessionId = sessionId,
            Type = AgentExecutionArtifactType.SessionState,
            Name = "WorkflowOutcome",
            AgentName = response.AgentName,
            Status = response.Success ? "Completed" : "Failed",
            Summary = response.Content,
            Data = new Dictionary<string, object>
            {
                ["latencyMs"] = latency.TotalMilliseconds,
                ["confidence"] = response.Confidence?.Value ?? 0d,
                ["toolsUsed"] = response.ToolsUsed,
                ["actions"] = response.ActionsPerformed,
                ["directRequest"] = directRequest,
                ["targetAgent"] = targetAgent ?? string.Empty
            }
        }, ct);

        try
        {
            await _sessionManager.ConsolidateSessionAsync(sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "⚠️ Falha ao consolidar sessão {SessionId}", sessionId);
        }
    }
}
