using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Core.Services;

public class AgentExecutionWorkflow : IAgentExecutionWorkflow
{
    private readonly IAgentFactory _agentFactory;
    private readonly ISessionManager _sessionManager;
    private readonly IConfidenceScoreCalculator _confidenceCalculator;
    private readonly IAgentRuntimeCoordinator _runtimeCoordinator;
    private readonly ILLMRuntimeContextAccessor _llmRuntimeContextAccessor;
    private readonly IFinalResponseApprovalService? _finalApprovalService;
    private readonly IQualityGateService? _qualityGateService;
    private readonly IReflectionEngine? _reflectionEngine;
    private readonly ICorrectionLoop? _correctionLoop;
    private readonly IAgentMemoryService? _agentMemoryService;
    private readonly IFrameworkOrchestratorService _frameworkOrchestrator;
    private readonly ILogger<AgentExecutionWorkflow> _logger;

    public AgentExecutionWorkflow(
        IAgentFactory agentFactory,
        ISessionManager sessionManager,
        IConfidenceScoreCalculator confidenceCalculator,
        IAgentRuntimeCoordinator runtimeCoordinator,
        ILLMRuntimeContextAccessor llmRuntimeContextAccessor,
        IFrameworkOrchestratorService frameworkOrchestrator,
        ILogger<AgentExecutionWorkflow> logger,
        IFinalResponseApprovalService? finalApprovalService = null,
        IQualityGateService? qualityGateService = null,
        IReflectionEngine? reflectionEngine = null,
        ICorrectionLoop? correctionLoop = null,
        IAgentMemoryService? agentMemoryService = null)
    {
        _agentFactory = agentFactory;
        _sessionManager = sessionManager;
        _confidenceCalculator = confidenceCalculator;
        _runtimeCoordinator = runtimeCoordinator;
        _llmRuntimeContextAccessor = llmRuntimeContextAccessor;
        _frameworkOrchestrator = frameworkOrchestrator;
        _logger = logger;
        _finalApprovalService = finalApprovalService;
        _qualityGateService = qualityGateService;
        _reflectionEngine = reflectionEngine;
        _correctionLoop = correctionLoop;
        _agentMemoryService = agentMemoryService;
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
