namespace AgenticSystem.Core.LLM.Models;

public class LLMResponse
{
    public string Content { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public UsageInfo Usage { get; set; } = new();
    public bool Success { get; set; } = true;
    public string? ErrorMessage { get; set; }
    public TimeSpan Latency { get; set; }

    public static LLMResponse Ok(string content, string model, string provider, UsageInfo? usage = null)
        => new()
        {
            Content = content,
            Model = model,
            Provider = provider,
            Usage = usage ?? new(),
            Success = true
        };

    public static LLMResponse Fail(string error, string provider)
        => new()
        {
            Success = false,
            ErrorMessage = error,
            Provider = provider
        };
}

public class UsageInfo
{
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int TotalTokens => PromptTokens + CompletionTokens;
    public decimal EstimatedCost { get; set; }
}
