using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using Microsoft.Extensions.AI.Evaluation.Quality;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Core.Services;

/// <summary>
/// Runtime evaluator contínuo — calcula scores, detecta regressões e gera alertas.
/// Opera sobre o histórico durável do IOperationalStore.
/// </summary>
public class RuntimeEvaluatorService : IRuntimeEvaluator
{
    private const int BaselineWindowSize = 20;
    private const double DefaultThreshold = 0.15;
    private const double DefaultBaseline = 0.7;

    private readonly IOperationalStore _store;
    private readonly IReflectionEngine _reflectionEngine;
    private readonly ISessionManager? _sessionManager;
    private readonly IChatClient? _evaluationChatClient;
    private readonly ILogger<RuntimeEvaluatorService> _logger;

    public RuntimeEvaluatorService(
        IOperationalStore store,
        IReflectionEngine reflectionEngine,
        ILogger<RuntimeEvaluatorService> logger,
        ISessionManager? sessionManager = null,
        IChatClient? evaluationChatClient = null)
    {
        _store = store;
        _reflectionEngine = reflectionEngine;
        _sessionManager = sessionManager;
        _evaluationChatClient = evaluationChatClient;
        _logger = logger;
    }

    public async Task<RuntimeEvaluationResult> EvaluateAsync(string? sessionId = null, string? agentName = null, CancellationToken ct = default)
    {
        var baseline = await GetBaselineAsync(agentName, ct);
        var factors = new Dictionary<string, double>();

        // Factor 1: Reflection confidence (from recent reflections)
        var reflections = await _reflectionEngine.GetRecentLearningsAsync(BaselineWindowSize);
        var reflectionList = reflections.ToList();
        var avgConfidence = reflectionList.Count > 0
            ? reflectionList.Average(r => r.ConfidenceInOutcome)
            : baseline;
        factors["reflectionConfidence"] = avgConfidence;

        // Factor 2: Error rate from metrics
        var metricsHistory = await _store.GetMetricsHistoryAsync(sessionId, limit: BaselineWindowSize, ct: ct);
        double errorRate = 0;
        double toolSuccessRate = 1.0;
        if (metricsHistory.Count > 0)
        {
            var latest = metricsHistory[0];
            var totalEvents = latest.EventsByType.Values.Sum();
            var errorEvents = latest.EventsByType.TryGetValue("Error", out var errors) ? errors : 0;
            errorRate = totalEvents > 0 ? (double)errorEvents / totalEvents : 0;
            factors["errorRate"] = 1.0 - errorRate;

            // Factor 3: Tool approval resolution rate
            if (latest.ToolApprovalsRequested > 0)
            {
                toolSuccessRate = (double)latest.ToolApprovalsResolved / latest.ToolApprovalsRequested;
            }
            factors["toolSuccessRate"] = toolSuccessRate;

            // Factor 4: Latency health (penalize if avg > 5s)
            var latencyScore = latest.AverageAgentLatencyMs switch
            {
                <= 1000 => 1.0,
                <= 3000 => 0.8,
                <= 5000 => 0.6,
                <= 10000 => 0.4,
                _ => 0.2
            };
            factors["latencyHealth"] = latencyScore;
        }
        else
        {
            factors["errorRate"] = 1.0;
            factors["toolSuccessRate"] = 1.0;
            factors["latencyHealth"] = 1.0;
        }

        // Factor 5: Deviation rate from reflections
        var deviationRate = reflectionList.Count > 0
            ? 1.0 - (double)reflectionList.Count(r => r.Deviations.Count > 0) / reflectionList.Count
            : 1.0;
        factors["deviationFreeRate"] = deviationRate;

        // Weighted overall score
        var heuristicScore =
            avgConfidence * 0.30 +
            factors["errorRate"] * 0.25 +
            toolSuccessRate * 0.15 +
            factors.GetValueOrDefault("latencyHealth", 1.0) * 0.15 +
            deviationRate * 0.15;

        var overallScore = heuristicScore;
        factors["heuristicScore"] = heuristicScore;

        var aiEvaluation = await EvaluateLastResponseWithAiAsync(sessionId, agentName, ct);
        if (aiEvaluation is not null)
        {
            factors["aiFluency"] = aiEvaluation.FluencyScore;
            factors["aiResponseQuality"] = aiEvaluation.ResponseQualityScore;

            overallScore =
                heuristicScore * 0.60 +
                aiEvaluation.FluencyScore * 0.15 +
                aiEvaluation.ResponseQualityScore * 0.25;
        }

        var regressionDetected = overallScore < baseline - DefaultThreshold;

        var alerts = new List<string>();
        if (regressionDetected)
            alerts.Add($"Regression detected: score {overallScore:F3} is below baseline {baseline:F3} - threshold {DefaultThreshold}");
        if (avgConfidence < 0.5)
            alerts.Add($"Low average confidence: {avgConfidence:F3}");
        if (errorRate > 0.1)
            alerts.Add($"High error rate: {errorRate:P1}");
        if (aiEvaluation is not null && aiEvaluation.FluencyScore < 0.55)
            alerts.Add($"Low AI fluency score: {aiEvaluation.FluencyScore:F3}");
        if (aiEvaluation is not null && aiEvaluation.ResponseQualityScore < 0.55)
            alerts.Add($"Low AI response quality score: {aiEvaluation.ResponseQualityScore:F3}");

        var result = new RuntimeEvaluationResult
        {
            SessionId = sessionId,
            AgentName = agentName,
            OverallScore = Math.Round(overallScore, 4),
            BaselineScore = Math.Round(baseline, 4),
            Threshold = DefaultThreshold,
            RegressionDetected = regressionDetected,
            Factors = factors,
            Alerts = alerts
        };

        await _store.SaveEvaluationAsync(result, ct);

        if (regressionDetected)
        {
            _logger.LogWarning("Runtime regression detected for agent {AgentName}: score={Score:F3}, baseline={Baseline:F3}",
                agentName ?? "global", overallScore, baseline);
        }

        return result;
    }

    public async Task<double> GetBaselineAsync(string? agentName = null, CancellationToken ct = default)
    {
        var history = await _store.GetEvaluationsAsync(agentName: agentName, limit: BaselineWindowSize, ct: ct);
        if (history.Count == 0)
            return DefaultBaseline;

        return history.Average(e => e.OverallScore);
    }

    public async Task<IReadOnlyList<RuntimeEvaluationResult>> DetectRegressionsAsync(DateTime? since = null, CancellationToken ct = default)
    {
        var from = since ?? DateTime.UtcNow.AddDays(-7);
        var evaluations = await _store.GetEvaluationsAsync(from: from, ct: ct);
        return evaluations.Where(e => e.RegressionDetected).ToList();
    }

    private async Task<AiEvaluationScores?> EvaluateLastResponseWithAiAsync(
        string? sessionId,
        string? agentName,
        CancellationToken ct)
    {
        if (_sessionManager is null || _evaluationChatClient is null || string.IsNullOrWhiteSpace(sessionId))
        {
            return null;
        }

        var recentEvents = await _sessionManager.GetRecentEventsAsync(sessionId, BaselineWindowSize);
        var targetEvent = recentEvents
            .Where(e => !string.IsNullOrWhiteSpace(e.UserInput) && !string.IsNullOrWhiteSpace(e.AgentResponse))
            .OrderByDescending(e => e.Timestamp)
            .FirstOrDefault(e => string.IsNullOrWhiteSpace(agentName) || e.AgentName.Equals(agentName, StringComparison.OrdinalIgnoreCase));

        if (targetEvent is null)
        {
            return null;
        }

        try
        {
#pragma warning disable AIEVAL001
            IEvaluator evaluator = new CompositeEvaluator(
                new FluencyEvaluator(),
                new RelevanceTruthAndCompletenessEvaluator());
#pragma warning restore AIEVAL001

            var userRequest = new ChatMessage(ChatRole.User, targetEvent.UserInput);
            var modelResponse = new ChatResponse(new ChatMessage(ChatRole.Assistant, targetEvent.AgentResponse));
            var chatConfiguration = new ChatConfiguration(_evaluationChatClient);

            var evaluationResult = await evaluator.EvaluateAsync(
                userRequest,
                modelResponse,
                chatConfiguration,
                cancellationToken: ct);

#pragma warning disable AIEVAL001
            var fluencyScore = GetNormalizedMetric(evaluationResult, FluencyEvaluator.FluencyMetricName);
            var rtcMetrics = new[]
            {
                GetNormalizedMetric(evaluationResult, RelevanceTruthAndCompletenessEvaluator.RelevanceMetricName),
                GetNormalizedMetric(evaluationResult, RelevanceTruthAndCompletenessEvaluator.TruthMetricName),
                GetNormalizedMetric(evaluationResult, RelevanceTruthAndCompletenessEvaluator.CompletenessMetricName)
            };
#pragma warning restore AIEVAL001

            var rtcValues = rtcMetrics.Where(value => value.HasValue).Select(value => value!.Value).ToList();
            if (!fluencyScore.HasValue && rtcValues.Count == 0)
            {
                return null;
            }

            return new AiEvaluationScores(
                FluencyScore: fluencyScore ?? 0.5,
                ResponseQualityScore: rtcValues.Count > 0 ? rtcValues.Average() : 0.5);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "AI runtime evaluation skipped for session {SessionId}", sessionId);
            return null;
        }
    }

    private static double? GetNormalizedMetric(EvaluationResult result, string metricName)
    {
        if (!result.Metrics.TryGetValue(metricName, out var metric) || metric is not NumericMetric numericMetric || !numericMetric.Value.HasValue)
        {
            return null;
        }

        return NormalizeScore(numericMetric.Value.Value);
    }

    private static double NormalizeScore(double rawScore)
    {
        if (rawScore <= 1.0)
        {
            return Math.Clamp(rawScore, 0.0, 1.0);
        }

        return Math.Clamp(rawScore / 5.0, 0.0, 1.0);
    }

    private sealed record AiEvaluationScores(double FluencyScore, double ResponseQualityScore);
}
