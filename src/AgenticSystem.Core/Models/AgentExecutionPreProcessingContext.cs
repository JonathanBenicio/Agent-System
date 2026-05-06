namespace AgenticSystem.Core.Models;

public class AgentExecutionPreProcessingContext
{
    public string SessionId { get; set; } = string.Empty;
    public string Input { get; set; } = string.Empty;
    public UserContext UserContext { get; set; } = new();
    public AnalysisResult? Analysis { get; set; }
    public string? TargetAgent { get; set; }
    public bool ValidateRequest { get; set; } = true;
    public bool ApplyCorrectionRules { get; set; } = true;
    public Dictionary<string, object> Metadata { get; set; } = new();
}

public class AgentExecutionPreProcessingResult
{
    public string EffectiveInput { get; set; } = string.Empty;
    public int AppliedCorrectionRuleCount { get; set; }
}