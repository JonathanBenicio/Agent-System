namespace AgenticSystem.Infrastructure.Configuration;

public sealed class ChatClientMiddlewareOptions
{
    public bool Enabled { get; set; } = true;
    public int MaxConcurrentRequests { get; set; } = 4;
    public int QueueWaitTimeoutSeconds { get; set; } = 30;
    public bool EnableRequestValidation { get; set; } = true;
    public bool EnableResponseValidation { get; set; } = true;
    public bool RejectInvalidResponses { get; set; } = true;
    public bool RejectInvalidStreamingResponses { get; set; }
}