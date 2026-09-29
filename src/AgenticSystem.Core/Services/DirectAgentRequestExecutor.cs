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
    private readonly ISessionStore? _sessionStore;
    private readonly ILLMRuntimeContextAccessor? _llmContextAccessor;

    public DirectAgentRequestExecutor(
        IAgentFactory agentFactory,
        IAgentExecutionPreProcessingPipeline preProcessingPipeline,
        ISessionManager sessionManager,
        IAgentRuntimeCoordinator runtimeCoordinator,
        IAgentExecutionPostProcessingPipeline postProcessingPipeline,
        ILogger<DirectAgentRequestExecutor> logger,
        IDirectAgentExecutionService? directAgentExecutionService = null,
        ISessionStore? sessionStore = null,
        ILLMRuntimeContextAccessor? llmContextAccessor = null)
    {
        _agentFactory = agentFactory;
        _preProcessingPipeline = preProcessingPipeline;
        _directAgentExecutionService = directAgentExecutionService;
        _sessionManager = sessionManager;
        _runtimeCoordinator = runtimeCoordinator;
        _postProcessingPipeline = postProcessingPipeline;
        _logger = logger;
        _sessionStore = sessionStore;
        _llmContextAccessor = llmContextAccessor;
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
            if (context.WorkflowOptions is { } workflowOptions)
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(context.UserId);
                ArgumentException.ThrowIfNullOrWhiteSpace(context.TenantId);
                sessionId = workflowOptions.SessionId;
                var store = _sessionStore ?? throw new InvalidOperationException("Workflow agent sessions require ISessionStore.");
                var existing = await store.GetAsync(sessionId, ct);
                if (existing is not null && (existing.UserId != context.UserId || existing.TenantId != context.TenantId))
                    throw new UnauthorizedAccessException("Workflow agent session belongs to another owner or tenant.");
                if (existing is null)
                    await store.SaveAsync(new SessionData
                    {
                        Id = sessionId, UserId = context.UserId, TenantId = context.TenantId, StartedAt = DateTime.UtcNow
                    }, ct);
            }
            using var executionScope = context.WorkflowOptions is null ? null : _runtimeCoordinator.BeginExecutionScope(sessionId, context);
            using var llmScope = context.WorkflowOptions is null ? null : _llmContextAccessor?.BeginScope(context, sessionId);
            IEnumerable<AgentInfo> agents;
            try
            {
                agents = await _agentFactory.GetAllAgentsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve agents from factory.");
                throw;
            }

            var agentInfo = agents.FirstOrDefault(a => a.Name.Equals(targetAgent, StringComparison.OrdinalIgnoreCase));

            if (agentInfo == null)
            {
                return AgentResponse.Error($"Agent '{targetAgent}' não encontrado ou inativo.", nameof(DirectAgentRequestExecutor));
            }

            var analysis = new AnalysisResult
            {
                EstimatedAgent = agentInfo.Name,
                Intent = IntentType.Chat,
                Confidence = 1.0,
                PrimaryDomain = agentInfo.Domain,
                RecommendedTier = agentInfo.Tier,
                RequiredTools = new List<string>(agentInfo.AvailableTools)
            };

            var selectedAgent = await _agentFactory.ResolveAgentAsync(agentInfo);

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
            var allowedTools = context.WorkflowOptions?.AllowedTools is { } restrictedTools
                ? selectedAgent.AvailableTools.Intersect(restrictedTools, StringComparer.OrdinalIgnoreCase)
                : selectedAgent.AvailableTools;
            using var agentScope = _runtimeCoordinator.BeginAgentScope(selectedAgent.Name, allowedTools);
            
            if (_directAgentExecutionService is null)
            {
                throw new InvalidOperationException("IDirectAgentExecutionService is not configured for direct execution.");
            }

            var response = await _directAgentExecutionService.ExecuteDirectAsync(
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

            return AgentResponse.Error("Erro interno ao processar requisição direta.", nameof(DirectAgentRequestExecutor));
        }
    }
}
