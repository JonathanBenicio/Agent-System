using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Infrastructure.Persistence.Entities;

[Table("eval_suite_results")]
public class EvalSuiteResultEntity
{
    [Key]
    [MaxLength(64)]
    public string SuiteId { get; set; } = string.Empty;

    [Required]
    [MaxLength(256)]
    public string AgentName { get; set; } = string.Empty;

    [MaxLength(64)]
    public string? AgentVersionId { get; set; }

    public int TotalTests { get; set; }

    public int Passed { get; set; }

    public int Failed { get; set; }

    public double OverallScore { get; set; }

    public double AccuracyScore { get; set; }

    public double SafetyScore { get; set; }

    public double LatencyP50Ms { get; set; }

    public double LatencyP95Ms { get; set; }

    public int TotalTokensUsed { get; set; }

    [Required]
    public string ResultsJson { get; set; } = "[]";

    [Required]
    public string RegressionsJson { get; set; } = "[]";

    public DateTime StartedAt { get; set; }

    public DateTime CompletedAt { get; set; }

    public EvalSuiteResult ToModel()
    {
        return new EvalSuiteResult
        {
            SuiteId = SuiteId,
            AgentName = AgentName,
            AgentVersionId = AgentVersionId,
            TotalTests = TotalTests,
            Passed = Passed,
            Failed = Failed,
            OverallScore = OverallScore,
            AccuracyScore = AccuracyScore,
            SafetyScore = SafetyScore,
            LatencyP50Ms = LatencyP50Ms,
            LatencyP95Ms = LatencyP95Ms,
            TotalTokensUsed = TotalTokensUsed,
            Results = string.IsNullOrWhiteSpace(ResultsJson)
                ? new List<EvalTestResult>()
                : System.Text.Json.JsonSerializer.Deserialize<List<EvalTestResult>>(ResultsJson) ?? new(),
            Regressions = string.IsNullOrWhiteSpace(RegressionsJson)
                ? new List<EvalRegressionAlert>()
                : System.Text.Json.JsonSerializer.Deserialize<List<EvalRegressionAlert>>(RegressionsJson) ?? new(),
            StartedAt = StartedAt,
            CompletedAt = CompletedAt
        };
    }

    public static EvalSuiteResultEntity FromModel(EvalSuiteResult model)
    {
        return new EvalSuiteResultEntity
        {
            SuiteId = model.SuiteId,
            AgentName = model.AgentName,
            AgentVersionId = model.AgentVersionId,
            TotalTests = model.TotalTests,
            Passed = model.Passed,
            Failed = model.Failed,
            OverallScore = model.OverallScore,
            AccuracyScore = model.AccuracyScore,
            SafetyScore = model.SafetyScore,
            LatencyP50Ms = model.LatencyP50Ms,
            LatencyP95Ms = model.LatencyP95Ms,
            TotalTokensUsed = model.TotalTokensUsed,
            ResultsJson = System.Text.Json.JsonSerializer.Serialize(model.Results),
            RegressionsJson = System.Text.Json.JsonSerializer.Serialize(model.Regressions),
            StartedAt = model.StartedAt,
            CompletedAt = model.CompletedAt
        };
    }
}
