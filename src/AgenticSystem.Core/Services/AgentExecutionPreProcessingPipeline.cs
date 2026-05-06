using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Core.Services;

public class AgentExecutionPreProcessingPipeline : IAgentExecutionPreProcessingPipeline
{
    private readonly IQualityGateService? _qualityGateService;
    private readonly ICorrectionLoop? _correctionLoop;
    private readonly ILogger<AgentExecutionPreProcessingPipeline> _logger;

    public AgentExecutionPreProcessingPipeline(
        ILogger<AgentExecutionPreProcessingPipeline> logger,
        IQualityGateService? qualityGateService = null,
        ICorrectionLoop? correctionLoop = null)
    {
        _logger = logger;
        _qualityGateService = qualityGateService;
        _correctionLoop = correctionLoop;
    }

    public async Task<AgentExecutionPreProcessingResult> ProcessAsync(
        AgentExecutionPreProcessingContext context,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(context.UserContext);
        ArgumentException.ThrowIfNullOrWhiteSpace(context.SessionId);

        await ValidateRequestAsync(context, ct);

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