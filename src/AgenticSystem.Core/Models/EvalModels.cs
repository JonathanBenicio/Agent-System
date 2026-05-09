namespace AgenticSystem.Core.Models;

// ═══════════════════════════════════════════════════════════
// Agent Evaluation — Test Suite & Scoring
// ═══════════════════════════════════════════════════════════

/// <summary>
/// A test case for evaluating an agent's response quality.
/// </summary>
public class EvalTestCase
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Name { get; init; } = string.Empty;
    public string AgentName { get; init; } = string.Empty;
    public string Input { get; init; } = string.Empty;
    public string? ExpectedOutput { get; init; }
    public List<string> ExpectedKeywords { get; init; } = [];
    public string? ForbiddenContent { get; init; }
    public EvalCategory Category { get; init; } = EvalCategory.Accuracy;
    public Dictionary<string, object> Metadata { get; init; } = new();
    public List<string> Tags { get; init; } = [];
}

/// <summary>
/// Result of running a single eval test case.
/// </summary>
public class EvalTestResult
{
    public string TestCaseId { get; init; } = string.Empty;
    public string TestCaseName { get; init; } = string.Empty;
    public string AgentName { get; init; } = string.Empty;
    public string ActualOutput { get; init; } = string.Empty;
    public bool Passed { get; init; }
    public double Score { get; init; } // 0.0 - 1.0
    public List<EvalMetric> Metrics { get; init; } = [];
    public TimeSpan Latency { get; init; }
    public int TokensUsed { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTime EvaluatedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// A single scored metric from an evaluation.
/// </summary>
public class EvalMetric
{
    public string Name { get; init; } = string.Empty;
    public double Score { get; init; }
    public double Weight { get; init; } = 1.0;
    public string? Details { get; init; }
}

/// <summary>
/// Aggregated result of running a full eval suite.
/// </summary>
public class EvalSuiteResult
{
    public string SuiteId { get; init; } = Guid.NewGuid().ToString("N");
    public string AgentName { get; init; } = string.Empty;
    public string? AgentVersionId { get; init; }
    public int TotalTests { get; init; }
    public int Passed { get; init; }
    public int Failed { get; init; }
    public double OverallScore { get; init; } // 0.0 - 1.0
    public double AccuracyScore { get; init; }
    public double SafetyScore { get; init; }
    public double LatencyP50Ms { get; init; }
    public double LatencyP95Ms { get; init; }
    public int TotalTokensUsed { get; init; }
    public List<EvalTestResult> Results { get; init; } = [];
    public List<EvalRegressionAlert> Regressions { get; init; } = [];
    public DateTime StartedAt { get; init; } = DateTime.UtcNow;
    public DateTime CompletedAt { get; set; }
    public TimeSpan Duration => CompletedAt - StartedAt;
}

/// <summary>
/// Alert raised when a test that previously passed now fails.
/// </summary>
public class EvalRegressionAlert
{
    public string TestCaseId { get; init; } = string.Empty;
    public string TestCaseName { get; init; } = string.Empty;
    public double PreviousScore { get; init; }
    public double CurrentScore { get; init; }
    public double ScoreDelta => CurrentScore - PreviousScore;
    public string Severity => ScoreDelta < -0.3 ? "Critical" : ScoreDelta < -0.1 ? "Warning" : "Info";
}

public enum EvalCategory
{
    Accuracy,
    Safety,
    Hallucination,
    ToolUsage,
    ResponseQuality,
    Latency,
    Guardrails,
    Regression
}
