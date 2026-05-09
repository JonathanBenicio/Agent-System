using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Interfaces;

/// <summary>
/// Service for running agent evaluation test suites.
/// Supports regression detection and scoring across multiple metrics.
/// </summary>
public interface IAgentEvaluationService
{
    /// <summary>
    /// Runs a single test case against an agent and returns the scored result.
    /// </summary>
    Task<EvalTestResult> EvaluateAsync(
        EvalTestCase testCase,
        CancellationToken ct = default);

    /// <summary>
    /// Runs a full test suite and returns aggregated results with regression detection.
    /// </summary>
    Task<EvalSuiteResult> RunSuiteAsync(
        IReadOnlyList<EvalTestCase> testCases,
        string? agentVersionId = null,
        CancellationToken ct = default);

    /// <summary>
    /// Detects regressions by comparing current results with a previous baseline.
    /// </summary>
    Task<IReadOnlyList<EvalRegressionAlert>> DetectRegressionsAsync(
        EvalSuiteResult current,
        EvalSuiteResult? baseline,
        CancellationToken ct = default);
}

/// <summary>
/// Persistence store for evaluation results and baselines.
/// </summary>
public interface IEvalResultStore
{
    Task SaveSuiteResultAsync(EvalSuiteResult result, CancellationToken ct = default);
    Task<EvalSuiteResult?> GetLatestBaselineAsync(string agentName, CancellationToken ct = default);
    Task<IReadOnlyList<EvalSuiteResult>> GetHistoryAsync(string agentName, int limit = 10, CancellationToken ct = default);
}
