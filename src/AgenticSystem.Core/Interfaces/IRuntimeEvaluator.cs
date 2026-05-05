namespace AgenticSystem.Core.Interfaces;

/// <summary>
/// Avaliador contínuo de qualidade do runtime — calcula scores, baselines e detecta regressões.
/// </summary>
public interface IRuntimeEvaluator
{
    Task<RuntimeEvaluationResult> EvaluateAsync(string? sessionId = null, string? agentName = null, CancellationToken ct = default);
    Task<double> GetBaselineAsync(string? agentName = null, CancellationToken ct = default);
    Task<IReadOnlyList<RuntimeEvaluationResult>> DetectRegressionsAsync(DateTime? since = null, CancellationToken ct = default);
}
