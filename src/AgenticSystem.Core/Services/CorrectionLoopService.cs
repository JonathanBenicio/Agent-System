using System.Collections.Concurrent;
using System.Text;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Core.Services;

/// <summary>
/// Maturity Level 5 — Gerencia correções humanas e regras persistentes.
/// </summary>
public class CorrectionLoopService : ICorrectionLoop
{
    private readonly ConcurrentDictionary<string, CorrectionRule> _rules = new();
    private readonly ConcurrentBag<HumanCorrection> _corrections = new();
    private readonly ILogger<CorrectionLoopService> _logger;

    public CorrectionLoopService(ILogger<CorrectionLoopService> logger)
    {
        _logger = logger;
    }

    public Task<CorrectionRule> AddRuleAsync(string userId, string rule, string? scope = null, string? targetAgent = null)
    {
        var correctionRule = new CorrectionRule
        {
            UserId = userId,
            Rule = rule,
            Scope = scope,
            TargetAgent = targetAgent
        };

        _rules[correctionRule.Id] = correctionRule;
        _logger.LogInformation("Correction rule added: {RuleId} for user {UserId}", correctionRule.Id, userId);

        return Task.FromResult(correctionRule);
    }

    public Task<IEnumerable<CorrectionRule>> GetActiveRulesAsync(string userId, string? agentName = null)
    {
        var rules = _rules.Values
            .Where(r => r.IsActive && r.UserId == userId)
            .Where(r => agentName == null || r.TargetAgent == null || r.TargetAgent == agentName)
            .OrderByDescending(r => r.TimesApplied)
            .AsEnumerable();

        return Task.FromResult(rules);
    }

    public Task<HumanCorrection> RecordCorrectionAsync(string sessionId, string original, string corrected, string? reason = null)
    {
        var correction = new HumanCorrection
        {
            SessionId = sessionId,
            OriginalResponse = original,
            CorrectedResponse = corrected,
            Reason = reason
        };

        _corrections.Add(correction);
        _logger.LogInformation("Human correction recorded for session {SessionId}", sessionId);

        return Task.FromResult(correction);
    }

    public Task DeactivateRuleAsync(string ruleId)
    {
        if (_rules.TryGetValue(ruleId, out var rule))
        {
            rule.IsActive = false;
            _logger.LogInformation("Correction rule {RuleId} deactivated", ruleId);
        }

        return Task.CompletedTask;
    }

    public Task<string> ApplyRulesToPromptAsync(string prompt, IEnumerable<CorrectionRule> rules)
    {
        var activeRules = rules.Where(r => r.IsActive).ToList();
        if (activeRules.Count == 0)
            return Task.FromResult(prompt);

        var sb = new StringBuilder();
        sb.AppendLine("## Human Correction Rules (must follow):");
        foreach (var rule in activeRules)
        {
            sb.AppendLine($"- {rule.Rule}");
            rule.TimesApplied++;
            rule.LastAppliedAt = DateTime.UtcNow;
        }
        sb.AppendLine();
        sb.Append(prompt);

        _logger.LogDebug("Applied {Count} correction rules to prompt", activeRules.Count);
        return Task.FromResult(sb.ToString());
    }
}
