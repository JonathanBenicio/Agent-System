using System.Text.Json.Serialization;

namespace AgenticSystem.Api.Controllers.OpenAI;

/// <summary>
/// Modelos compatíveis com a API OpenAI /v1/chat/completions.
/// Permite que ferramentas externas interajam com os agentes do sistema
/// usando o formato padrão da OpenAI.
/// </summary>

public sealed class ChatCompletionRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = "agentic-system";

    [JsonPropertyName("messages")]
    public List<ChatCompletionMessage> Messages { get; set; } = new();

    [JsonPropertyName("temperature")]
    public double? Temperature { get; set; }

    [JsonPropertyName("max_tokens")]
    public int? MaxTokens { get; set; }

    [JsonPropertyName("stream")]
    public bool Stream { get; set; }

    [JsonPropertyName("user")]
    public string? User { get; set; }
}

public sealed class ChatCompletionMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = "user";

    [JsonPropertyName("content")]
    public string? Content { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

public sealed class ChatCompletionResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("object")]
    public string Object { get; set; } = "chat.completion";

    [JsonPropertyName("created")]
    public long Created { get; set; }

    [JsonPropertyName("model")]
    public string Model { get; set; } = "agentic-system";

    [JsonPropertyName("choices")]
    public List<ChatCompletionChoice> Choices { get; set; } = new();

    [JsonPropertyName("usage")]
    public ChatCompletionUsage? Usage { get; set; }

    [JsonPropertyName("system_fingerprint")]
    public string? SystemFingerprint { get; set; }
}

public sealed class ChatCompletionChoice
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("message")]
    public ChatCompletionMessage? Message { get; set; }

    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; set; }
}

public sealed class ChatCompletionUsage
{
    [JsonPropertyName("prompt_tokens")]
    public int PromptTokens { get; set; }

    [JsonPropertyName("completion_tokens")]
    public int CompletionTokens { get; set; }

    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; set; }
}

public sealed class ChatCompletionError
{
    [JsonPropertyName("error")]
    public ChatCompletionErrorDetail Error { get; set; } = new();
}

public sealed class ChatCompletionErrorDetail
{
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = "invalid_request_error";

    [JsonPropertyName("param")]
    public string? Param { get; set; }

    [JsonPropertyName("code")]
    public string? Code { get; set; }
}
