namespace AgenticSystem.Infrastructure.Configuration;

public sealed class CollaborationWorkflowOptions
{
    public bool EnableAdvancedWorkflow { get; set; }
    public bool EnableConcurrentContextStage { get; set; } = true;
    public bool EnableCheckpointing { get; set; } = true;
    public bool EnableNativeHandoffReview { get; set; }
    public bool EnableNativeGroupChatTermination { get; set; }
    public int GroupChatMaximumIterations { get; set; } = 4;
    public string[] GroupChatTerminationPhrases { get; set; } =
    [
        "review completed",
        "final recommendation",
        "final answer",
        "approved"
    ];
}