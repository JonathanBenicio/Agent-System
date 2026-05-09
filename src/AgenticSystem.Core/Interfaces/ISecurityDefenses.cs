using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Interfaces;

/// <summary>
/// #31 — Autonomy enforcement service.
/// Controls what agents can do independently based on configured autonomy levels.
/// </summary>
public interface IAutonomyEnforcer
{
    Task<AutonomyConfig> GetConfigAsync(string agentName, CancellationToken ct = default);
    Task SetConfigAsync(AutonomyConfig config, CancellationToken ct = default);
    Task<bool> IsActionAllowedAsync(string agentName, string actionType, double estimatedCost = 0, CancellationToken ct = default);
}

/// <summary>
/// #32 — Dynamic risk scoring service.
/// Evaluates the risk of an action in real-time.
/// </summary>
public interface IRiskScorer
{
    Task<RiskAssessment> AssessRiskAsync(string agentName, string actionDescription, Dictionary<string, object>? context = null, CancellationToken ct = default);
}

/// <summary>
/// #35 — Prompt injection defense service.
/// Detects and mitigates prompt injection attempts.
/// </summary>
public interface IPromptInjectionDefense
{
    Task<PromptInjectionAnalysis> AnalyzeAsync(string input, CancellationToken ct = default);
    Task<string> SanitizeAsync(string input, CancellationToken ct = default);
}

/// <summary>
/// #36 — Data loss prevention service.
/// Detects and handles PII/sensitive data in text.
/// </summary>
public interface IDlpService
{
    Task<DlpScanResult> ScanAsync(string text, CancellationToken ct = default);
    Task<string> RedactAsync(string text, CancellationToken ct = default);
    Task<string> MaskAsync(string text, CancellationToken ct = default);
}
