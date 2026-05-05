namespace AgenticSystem.Core.Models;

public enum AgentChannelKind
{
    Direct,
    FanOut,
    Chain,
    Review,
    Planner,
    Broadcast
}

public class AgentChannelMessage
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string SessionId { get; set; } = string.Empty;
    public string SourceAgent { get; set; } = string.Empty;
    public string TargetAgent { get; set; } = string.Empty;
    public AgentChannelKind Kind { get; set; } = AgentChannelKind.Direct;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Dictionary<string, object> Metadata { get; set; } = new();
}