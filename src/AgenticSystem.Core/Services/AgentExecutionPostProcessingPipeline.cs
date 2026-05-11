using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Core.Services;

public class AgentExecutionPostProcessingPipeline : IAgentExecutionPostProcessingPipeline
{
    private readonly ISessionManager _sessionManager;
    private readonly IAgentRuntimeCoordinator _runtimeCoordinator;
    private readonly IConfidenceScoreCalculator _confidenceCalculator;
    private readonly IFinalResponseApprovalService? _finalApprovalService;
    private readonly IQualityGateService? _qualityGateService;
    private readonly IReflectionEngine? _reflectionEngine;
    private readonly ICorrectionLoop? _correctionLoop;
    private readonly IAgentMemoryService? _agentMemoryService;
    private readonly ICitationEngine? _citationEngine;
    private readonly IAgentEvaluationService? _evaluationService;
    private readonly ISelfImprovementEngine? _selfImprovementEngine;
    private readonly IPromptManager? _promptManager;
    private readonly IAgentVersioningService? _versioningService;
    private readonly ILogger<AgentExecutionPostProcessingPipeline> _logger;

    public AgentExecutionPostProcessingPipeline(
        ISessionManager sessionManager,
        IAgentRuntimeCoordinator runtimeCoordinator,
        IConfidenceScoreCalculator confidenceCalculator,
        ILogger<AgentExecutionPostProcessingPipeline> logger,
        IFinalResponseApprovalService? finalApprovalService = null,
        IQualityGateService? qualityGateService = null,
        IReflectionEngine? reflectionEngine = null,
        ICorrectionLoop? correctionLoop = null,
        IAgentMemoryService? agentMemoryService = null,
        ICitationEngine? citationEngine = null,
        IAgentEvaluationService? evaluationService = null,
        ISelfImprovementEngine? selfImprovementEngine = null,
        IPromptManager? promptManager = null,
        IAgentVersioningService? versioningService = null)
    {
        _sessionManager = sessionManager;
        _runtimeCoordinator = runtimeCoordinator;
        _confidenceCalculator = confidenceCalculator;
        _logger = logger;
        _finalApprovalService = finalApprovalService;
        _qualityGateService = qualityGateService;
        _reflectionEngine = reflectionEngine;
        _correctionLoop = correctionLoop;
        _agentMemoryService = agentMemoryService;
        _citationEngine = citationEngine;
        _evaluationService = evaluationService;
        _selfImprovementEngine = selfImprovementEngine;
        _promptManager = promptManager;
        _versioningService = versioningService;
    }

    public async Task<AgentResponse> ProcessAsync(
        AgentExecutionPostProcessingContext context,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(context.Analysis);
        ArgumentNullException.ThrowIfNull(context.Response);
        ArgumentNullException.ThrowIfNull(context.UserContext);
        ArgumentException.ThrowIfNullOrWhiteSpace(context.SessionId);

        var response = context.Response;
        response.SessionId = context.SessionId;

        if (string.IsNullOrWhiteSpace(response.AgentName))
        {
            response.AgentName = context.Analysis.EstimatedAgent;
        }

        // Apply citations if RAG context is present
        if (_citationEngine is not null && context.RagContext is { Chunks.Count: > 0 } && !string.IsNullOrWhiteSpace(response.Content))
        {
            try
            {
                var cited = await _citationEngine.GenerateWithCitationsAsync(response.Content, context.RagContext.Chunks, ct);
                response.Content = cited.CitedText;
                response.Metadata["citations"] = cited.Citations;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "⚠️ Failure generating citations for session {SessionId}", context.SessionId);
            }
        }

        await ValidatePostExecutionAsync(context, ct);

        // 1. Evaluate Response (Phase 3 Integration)
        if (_evaluationService != null)
        {
            var testCase = new EvalTestCase
            {
                AgentName = response.AgentName,
                Input = context.Input,
                ExpectedOutput = null // Ground truth unknown in runtime
            };
            var evalResult = await _evaluationService.EvaluateAsync(testCase, ct);
            _logger.LogInformation("Execution Evaluated. Score: {Score}", evalResult.Score);
            response.Metadata["evaluationScore"] = evalResult.Score;
        }

        var reflectionOutcome = await ReflectAsync(context);
        response.Confidence = _confidenceCalculator.Calculate(
            response,
            ragContext: context.RagContext,
            reflections: reflectionOutcome.Reflections,
            toolAvailability: null);

        // 2. Self-Improvement & Versioning (Phase 3 Integration)
        if (_selfImprovementEngine != null && reflectionOutcome.LatestReflection != null && _promptManager != null && _versioningService != null)
        {
            var improvement = await _selfImprovementEngine.AnalyzeAndImproveAsync(response.AgentName, ct);
            if (improvement.Status == "Proposed" && improvement.ProposedChanges.TryGetValue("instructions_update", out var newInstructions))
            {
                _logger.LogInformation("Proposed improvement found for {Agent}. Updating template.", response.AgentName);
                
                await _promptManager.SaveTemplateAsync(new PromptTemplate
                {
                    AgentName = response.AgentName,
                    TemplateBody = newInstructions,
                    Name = "Auto-improved Template",
                    Description = $"Improved based on reflection in session {context.SessionId}",
                    CreatedBy = "SelfImprovementEngine"
                }, ct);

                await _versioningService.CreateVersionAsync(
                    response.AgentName, 
                    description: $"Auto-improved via reflection in session {context.SessionId}", 
                    changeLog: improvement.Rationale, 
                    createdBy: "System", 
                    ct: ct);

                await _selfImprovementEngine.ApplyImprovementAsync(improvement.Id, ct);
            }
        }

        var approvalResponse = await ApplyFinalApprovalAsync(context, ct);
        if (approvalResponse is not null)
        {
            return approvalResponse;
        }

        await PersistExecutionResultAsync(context, ct);

        if (_agentMemoryService is not null)
        {
            await _agentMemoryService.RecordInteractionAsync(
                context.SessionId,
                response.AgentName,
                context.UserContext,
                context.Input,
                response,
                reflectionOutcome.LatestReflection,
                ct);
        }

        return response;
    }

    private async Task ValidatePostExecutionAsync(
        AgentExecutionPostProcessingContext context,
        CancellationToken ct)
    {
        if (!context.ValidateResponse || _qualityGateService is null)
        {
            return;
        }

        await _qualityGateService.ValidateResponseAsync(
            context.Input,
            context.Response.Content ?? string.Empty,
            ct: ct);
    }

    private async Task<(IEnumerable<Reflection>? Reflections, Reflection? LatestReflection)> ReflectAsync(
        AgentExecutionPostProcessingContext context)
    {
        if (_reflectionEngine is null)
        {
            return (null, null);
        }

        if (!context.RunReflection)
        {
            var existingReflections = await _reflectionEngine.GetSessionReflectionsAsync(context.SessionId);
            return (existingReflections, null);
        }

        var latestReflection = await _reflectionEngine.ReflectAsync(
            context.SessionId,
            context.Response.AgentName,
            context.Input,
            context.Response.Content ?? string.Empty,
            context.Response.Success ? 0.85 : 0.25);
        var reflections = await _reflectionEngine.GetSessionReflectionsAsync(context.SessionId);

        if (context.LearnFromReflection
            && _correctionLoop is not null
            && !string.IsNullOrWhiteSpace(latestReflection.ImprovementSuggestion))
        {
            await _correctionLoop.AddRuleAsync(
                context.UserContext.UserId,
                latestReflection.ImprovementSuggestion,
                scope: "auto-reflection",
                targetAgent: context.Response.AgentName);
        }

        return (reflections, latestReflection);
    }

    private async Task<AgentResponse?> ApplyFinalApprovalAsync(
        AgentExecutionPostProcessingContext context,
        CancellationToken ct)
    {
        if (_finalApprovalService is null)
        {
            return null;
        }

        var decision = await _finalApprovalService.EvaluateAsync(
            context.SessionId,
            context.Input,
            context.Analysis,
            context.Response,
            ct);
        if (decision.Allowed || !decision.RequiresApproval || decision.ApprovalRequest is null)
        {
            return null;
        }

        var pendingResponse = new AgentResponse
        {
            Success = false,
            AgentName = context.Response.AgentName,
            AgentTier = context.Response.AgentTier,
            SessionId = context.SessionId,
            Content = "Resposta gerada e aguardando aprovação humana antes da publicação final.",
            Metadata = new Dictionary<string, object>(context.Response.Metadata)
            {
                ["pendingFinalApproval"] = true,
                ["finalApprovalId"] = decision.ApprovalRequest.Id,
                ["finalApprovalReason"] = decision.Reason,
                ["proposedResponse"] = context.Response.Content,
                ["approvalKind"] = "final-response"
            },
            ActionsPerformed = context.Response.ActionsPerformed.ToList(),
            ToolsUsed = context.Response.ToolsUsed.ToList(),
            Confidence = context.Response.Confidence
        };

        await _sessionManager.AddEventAsync(context.SessionId, new AgentEvent
        {
            SessionId = context.SessionId,
            AgentName = context.Response.AgentName,
            AgentTier = context.Response.AgentTier,
            UserInput = context.Input,
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
        AgentExecutionPostProcessingContext context,
        CancellationToken ct)
    {
        var eventContext = new Dictionary<string, object>
        {
            ["analysis"] = context.Analysis,
            ["user_context"] = context.UserContext,
            ["executionMode"] = context.Response.Metadata.TryGetValue("executionMode", out var mode)
                ? mode?.ToString() ?? "single-agent"
                : "single-agent",
            ["directRequest"] = context.DirectRequest,
            ["targetAgent"] = context.TargetAgent ?? string.Empty
        };

        Merge(eventContext, context.EventContext);

        await _sessionManager.AddEventAsync(context.SessionId, new AgentEvent
        {
            SessionId = context.SessionId,
            AgentName = context.Response.AgentName,
            AgentTier = context.Response.AgentTier,
            UserInput = context.Input,
            AgentResponse = context.Response.Content ?? string.Empty,
            ActionsPerformed = context.Response.ActionsPerformed,
            ToolsUsed = context.Response.ToolsUsed,
            Context = eventContext
        });

        var artifactData = new Dictionary<string, object>
        {
            ["latencyMs"] = context.Latency.TotalMilliseconds,
            ["confidence"] = context.Response.Confidence?.Value ?? 0d,
            ["toolsUsed"] = context.Response.ToolsUsed,
            ["actions"] = context.Response.ActionsPerformed,
            ["directRequest"] = context.DirectRequest,
            ["targetAgent"] = context.TargetAgent ?? string.Empty
        };

        Merge(artifactData, context.ArtifactData);

        await _runtimeCoordinator.RecordArtifactAsync(new AgentExecutionArtifact
        {
            SessionId = context.SessionId,
            Type = AgentExecutionArtifactType.SessionState,
            Name = "WorkflowOutcome",
            AgentName = context.Response.AgentName,
            Status = context.Response.Success ? "Completed" : "Failed",
            Summary = context.Response.Content,
            Data = artifactData
        }, ct);

        try
        {
            await _sessionManager.ConsolidateSessionAsync(context.SessionId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "⚠️ Falha ao consolidar sessão {SessionId}", context.SessionId);
        }
    }

    private static void Merge(
        Dictionary<string, object> target,
        Dictionary<string, object>? extra)
    {
        if (extra is null)
        {
            return;
        }

        foreach (var entry in extra)
        {
            target[entry.Key] = entry.Value;
        }
    }
}