using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Core.Services;

public class AgentExecutionPreProcessingPipeline : IAgentExecutionPreProcessingPipeline
{
    private readonly IQualityGateService? _qualityGateService;
    private readonly ICorrectionLoop? _correctionLoop;
    private readonly IQuotaEnforcer? _quotaEnforcer;
    private readonly ISetupFlowManager? _setupFlowManager;
    private readonly IModelRouter? _modelRouter;
    private readonly IAgentSandbox? _agentSandbox;
    private readonly ILogger<AgentExecutionPreProcessingPipeline> _logger;

    public AgentExecutionPreProcessingPipeline(
        ILogger<AgentExecutionPreProcessingPipeline> logger,
        IQualityGateService? qualityGateService = null,
        ICorrectionLoop? correctionLoop = null,
        IQuotaEnforcer? quotaEnforcer = null,
        ISetupFlowManager? setupFlowManager = null,
        IModelRouter? modelRouter = null,
        IAgentSandbox? agentSandbox = null)
    {
        _logger = logger;
        _qualityGateService = qualityGateService;
        _correctionLoop = correctionLoop;
        _quotaEnforcer = quotaEnforcer;
        _setupFlowManager = setupFlowManager;
        _modelRouter = modelRouter;
        _agentSandbox = agentSandbox;
    }

    public async Task<AgentExecutionPreProcessingResult> ProcessAsync(
        AgentExecutionPreProcessingContext context,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(context.UserContext);
        ArgumentException.ThrowIfNullOrWhiteSpace(context.SessionId);

        // 1. Quota & Quality Validation
        await ValidateRequestAsync(context, ct);

        // 2. Setup Flow Short-circuit (ML15)
        if (_setupFlowManager != null && await _setupFlowManager.IsInSetupFlowAsync(context.UserContext.UserId))
        {
            _logger.LogInformation("Short-circuiting to Setup Flow for user {UserId}", context.UserContext.UserId);
            context.Metadata["shortCircuitToSetup"] = true;
        }

        // 3. Adaptive Model Routing (Phase 3)
        if (_modelRouter != null && context.Analysis != null)
        {
            var routingRequest = new ModelRoutingRequest
            {
                TaskDescription = context.Input,
                Priority = ModelRoutingPriority.Balanced
            };

            var route = await _modelRouter.RouteToModelAsync(routingRequest, ct);
            if (route != null)
            {
                context.UserContext.Preferences["llm.model"] = route.ModelId;
                context.UserContext.Preferences["llm.provider"] = route.Provider;
                _logger.LogDebug("Adaptive Routing selected {Provider}/{Model}", route.Provider, route.ModelId);
            }
        }

        // 4. Sandbox Activation (ML22)
        if (_agentSandbox != null && !string.IsNullOrEmpty(context.TargetAgent))
        {
            // Simple heuristic: if agent name starts with "test" or "dev", or metadata requires it
            if (context.TargetAgent.StartsWith("Test", StringComparison.OrdinalIgnoreCase) || 
                context.Metadata.ContainsKey("useSandbox"))
            {
                var sbConfig = await _agentSandbox.CreateSandboxAsync(context.TargetAgent, null, ct);
                context.Metadata["sandboxId"] = sbConfig.Id;
                _logger.LogInformation("Enabled sandbox {SandboxId} for execution", sbConfig.Id);
            }
        }

        var effectiveInput = context.Input;
        var appliedRuleCount = 0;

        if (context.ApplyCorrectionRules
            && _correctionLoop is not null
            && !string.IsNullOrWhiteSpace(context.UserContext.UserId))
        {
            var rules = (await _correctionLoop.GetActiveRulesAsync(context.UserContext.UserId, context.TargetAgent)).ToList();
            appliedRuleCount = rules.Count;

            if (appliedRuleCount > 0)
            {
                effectiveInput = await _correctionLoop.ApplyRulesToPromptAsync(context.Input, rules);
                _logger.LogDebug(
                    "Applied {RuleCount} correction rule(s) for session {SessionId} and target agent {TargetAgent}",
                    appliedRuleCount,
                    context.SessionId,
                    context.TargetAgent ?? "(global)");
            }
        }

        return new AgentExecutionPreProcessingResult
        {
            EffectiveInput = effectiveInput,
            AppliedCorrectionRuleCount = appliedRuleCount
        };
    }

    private async Task ValidateRequestAsync(
        AgentExecutionPreProcessingContext context,
        CancellationToken ct)
    {
        // 1. Enforce Quotas (Phase 5)
        if (_quotaEnforcer != null)
        {
            var quotaCheck = await _quotaEnforcer.CheckQuotaAsync(context.UserContext.TenantId ?? context.UserContext.UserId, ct: ct);
            if (!quotaCheck.Allowed)
            {
                throw new InvalidOperationException($"Quota Exceeded: {quotaCheck.DenialReason}");
            }
        }

        if (!context.ValidateRequest)
        {
            return;
        }

        if (_qualityGateService is not null)
        {
            var qualityReport = await _qualityGateService.ValidateRequestAsync(context.Input, context.Metadata, ct);
            if (!qualityReport.OverallPassed)
            {
                var issues = string.Join(
                    "; ",
                    qualityReport.Results
                        .Where(result => !result.Passed)
                        .SelectMany(result => result.Issues)
                        .Where(issue => !string.IsNullOrWhiteSpace(issue)));

                throw new InvalidOperationException(issues);
            }

            return;
        }

        if (string.IsNullOrWhiteSpace(context.Input))
        {
            throw new InvalidOperationException("Input não pode estar vazio.");
        }

        if (context.Input.Length > 10_000)
        {
            throw new InvalidOperationException("Input muito longo (máximo 10.000 caracteres).");
        }

        if (context.Analysis is not null && context.Analysis.Confidence < 0.3)
        {
            throw new InvalidOperationException("Não foi possível entender a solicitação. Tente ser mais específico.");
        }
    }
}