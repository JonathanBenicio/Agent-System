using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AgenticSystem.Core.Models;

public class GoldenSetCaseDto
{
    public string Id { get; set; } = string.Empty;
    public string Input { get; set; } = string.Empty;
    public string ExpectedOutput { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
}

public class GoldenSetDto
{
    public string Id { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string AgentName { get; set; } = string.Empty;
    public List<GoldenSetCaseDto> Cases { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CreateGoldenSetDto
{
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string Description { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string AgentName { get; set; } = string.Empty;

    [Required]
    public List<GoldenSetCaseDto> Cases { get; set; } = new();
}

public class UpdateGoldenSetDto
{
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string Description { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string AgentName { get; set; } = string.Empty;

    [Required]
    public List<GoldenSetCaseDto> Cases { get; set; } = new();
}

public class GoldenSetRunResultDto
{
    public string SuiteId { get; set; } = string.Empty;
    public string AgentName { get; set; } = string.Empty;
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
    public List<EvalTestResult> Results { get; set; } = new();
    public List<EvalRegressionAlert> Regressions { get; set; } = new();
    public DateTime StartedAt { get; set; }
    public DateTime CompletedAt { get; set; }
}

public class GoldenSetRunStatusDto
{
    public string RunId { get; set; } = string.Empty;
    public string GoldenSetId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty; // "Running", "Completed", or "Failed"
    public int TotalTests { get; set; }
    public int CompletedTests { get; set; }
    public string? ErrorMessage { get; set; }
    public GoldenSetRunResultDto? Result { get; set; }
}
