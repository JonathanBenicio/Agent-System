using Microsoft.Extensions.Logging;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Services;

/// <summary>
/// Encapsula o caminho de execução direta para manter o workflow principal como casca fina.
/// </summary>
public class DirectAgentRequestExecutor : IDirectAgentRequestExecutor
{
    private readonly IAgentFactory _agentFactory;
    private readonly IDirectAgentExecutionService? _directAgentExecutionService;
    private readonly IAgentExecutionPreProcessingPipeline _preProcessingPipeline;
    private readonly ISessionManager _sessionManager;
    private readonly IAgentRuntimeCoordinator _runtimeCoordinator;
    private readonly IAgentExecutionPostProcessingPipeline _postProcessingPipeline;
    private readonly ILogger<DirectAgentRequestExecutor> _logger;

    public DirectAgentRequestExecutor(
        IAgentFactory agentFactory,
        IAgentExecutionPreProcessingPipeline preProcessingPipeline,
        ISessionManager sessionManager,
        IAgentRuntimeCoordinator runtimeCoordinator,
        IAgentExecutionPostProcessingPipeline postProcessingPipeline,
        ILogger<DirectAgentRequestExecutor> logger,
        IDirectAgentExecutionService? directAgentExecutionService = null)
    {
        _agentFactory = agentFactory;
        _preProcessingPipeline = preProcessingPipeline;
        _directAgentExecutionService = directAgentExecutionService;
        _sessionManager = sessionManager;
        _runtimeCoordinator = runtimeCoordinator;
        _postProcessingPipeline = postProcessingPipeline;
        _logger = logger;
    }

    public async Task<AgentResponse> ExecuteAsync(
        string sessionId,
        string input,
        UserContext context,
        string targetAgent,
        CancellationToken ct = default)
    {
        try
        {
            var agents = await _agentFactory.GetAllAgentsAsync();
            var requestedAgent = agents.FirstOrDefault(agent => agent.Name.Equals(targetAgent, StringComparison.OrdinalIgnoreCase));
            if (requestedAgent is null)
            {
                return AgentResponse.Error($"Agent '{targetAgent}' não encontrado.", nameof(DirectAgentRequestExecutor));
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

            var selectedAgent = await _agentFactory.ResolveAgentAsync(requestedAgent);
            var preProcessingResult = await _preProcessingPipeline.ProcessAsync(new AgentExecutionPreProcessingContext
            {
                SessionId = sessionId,
                Input = input,
                UserContext = context,
                Analysis = analysis,
                TargetAgent = selectedAgent.Name,
                ValidateRequest = true,
                ApplyCorrectionRules = true,
                Metadata = new Dictionary<string, object>
                {
                    ["executionMode"] = "direct",
                    ["targetAgent"] = targetAgent
                }
            }, ct);

            await _runtimeCoordinator.PublishEventAsync(new AgentStreamEvent
            {
                Type = AgentStreamEventType.AgentSelected,
                AgentName = selectedAgent.Name,
                Message = selectedAgent.Description,
                Data = new Dictionary<string, object>
                {
                    ["directRequest"] = true,
                    ["targetAgent"] = targetAgent
                }
            }, ct);

            var executionSw = System.Diagnostics.Stopwatch.StartNew();
            using var agentScope = _runtimeCoordinator.BeginAgentScope(selectedAgent.Name, selectedAgent.AvailableTools);
            var response = _directAgentExecutionService is null
                ? await selectedAgent.ExecuteAsync(preProcessingResult.EffectiveInput, context)
                : await _directAgentExecutionService.ExecuteDirectAsync(
                    selectedAgent,
                    sessionId,
                    preProcessingResult.EffectiveInput,
                    context,
                    ct);
            executionSw.Stop();

            response.SessionId = sessionId;
            if (string.IsNullOrWhiteSpace(response.AgentName))
            {
                response.AgentName = selectedAgent.Name;
            }

            if (response.AgentTier == default)
            {
                response.AgentTier = selectedAgent.Tier;
            }

            response.Metadata["executionMode"] = "direct";
            response.Metadata["appliedCorrectionRules"] = preProcessingResult.AppliedCorrectionRuleCount;

            return await _postProcessingPipeline.ProcessAsync(new AgentExecutionPostProcessingContext
            {
                SessionId = sessionId,
                Input = input,
                UserContext = context,
                Analysis = analysis,
                Response = response,
                Latency = executionSw.Elapsed,
                DirectRequest = true,
                TargetAgent = targetAgent,
                ValidateResponse = true,
                RunReflection = true,
                LearnFromReflection = true
            }, ct);
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

            return AgentResponse.Error("Erro interno ao processar requisição direta.", nameof(DirectAgentRequestExecutor));
        }
    }
}