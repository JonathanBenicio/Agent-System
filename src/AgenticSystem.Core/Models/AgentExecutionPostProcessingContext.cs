namespace AgenticSystem.Core.Models;

public class AgentExecutionPostProcessingContext
{
    public string SessionId { get; set; } = string.Empty;
    public string Input { get; set; } = string.Empty;
    public UserContext UserContext { get; set; } = new();
    public AnalysisResult Analysis { get; set; } = new();
    public AgentResponse Response { get; set; } = new();
    public TimeSpan Latency { get; set; }
    public bool DirectRequest { get; set; }
    public string? TargetAgent { get; set; }
    public bool ValidateResponse { get; set; } = true;
    public bool RunReflection { get; set; } = true;
    public bool LearnFromReflection { get; set; } = true;
    public Dictionary<string, object> EventContext { get; set; } = new();
    public Dictionary<string, object> ArtifactData { get; set; } = new();
}