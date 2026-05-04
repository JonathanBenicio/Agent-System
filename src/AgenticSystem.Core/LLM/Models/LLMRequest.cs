namespace AgenticSystem.Core.LLM.Models;

public class LLMRequest
{
    public string Prompt { get; set; } = string.Empty;
    public string? SystemPrompt { get; set; }
    public string? Model { get; set; }
    public string? Provider { get; set; }
    public LLMParameters Parameters { get; set; } = new();
    public List<ChatMessage> Messages { get; set; } = new();
    public ResponseFormat? ResponseFormat { get; set; }
}

public class ChatMessage
{
    public string Role { get; set; } = "user";
    public string Content { get; set; } = string.Empty;

    public static ChatMessage System(string content) => new() { Role = "system", Content = content };
    public static ChatMessage User(string content) => new() { Role = "user", Content = content };
    public static ChatMessage Assistant(string content) => new() { Role = "assistant", Content = content };
}

public class LLMParameters
{
    public double Temperature { get; set; } = 0.7;
    public int MaxTokens { get; set; } = 2000;
    public double TopP { get; set; } = 1.0;
    public double FrequencyPenalty { get; set; } = 0.0;
    public double PresencePenalty { get; set; } = 0.0;
    public List<string>? Stop { get; set; }
}

public enum ResponseFormat
{
    Text,
    Json
}
