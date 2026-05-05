using AgenticSystem.Core.Interfaces;

namespace AgenticSystem.Core.Services;

/// <summary>
/// No-op in-memory fallback for IRuntimeEvaluator when PostgreSQL operational store is not configured.
/// Returns neutral defaults — no regressions, baseline of 0.8.
/// </summary>
public class InMemoryRuntimeEvaluator : IRuntimeEvaluator
{
    private const double DefaultBaseline = 0.8;

    public Task<RuntimeEvaluationResult> EvaluateAsync(string? sessionId = null, string? agentName = null, CancellationToken ct = default)
    {
        return Task.FromResult(new RuntimeEvaluationResult
        {
            SessionId = sessionId,
            AgentName = agentName,
            OverallScore = DefaultBaseline,
            BaselineScore = DefaultBaseline,
            Threshold = 0.6,
            RegressionDetected = false
        });
    }

    public Task<double> GetBaselineAsync(string? agentName = null, CancellationToken ct = default)
    {
        return Task.FromResult(DefaultBaseline);
    }

    public Task<IReadOnlyList<RuntimeEvaluationResult>> DetectRegressionsAsync(DateTime? since = null, CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<RuntimeEvaluationResult>>(Array.Empty<RuntimeEvaluationResult>());
    }
}
