namespace AgenticSystem.Core.LLM.Models;

public class LLMProviderApiKey
{
    public string Id { get; set; } = string.Empty;
    public string ProviderName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DecryptedValue { get; set; } = string.Empty;
    public string LastFour { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public bool IsDefault { get; set; }
    public IReadOnlyList<string> Models { get; set; } = Array.Empty<string>();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class RegisterApiKeyRequest
{
    public string Name { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
}

public class UpdateApiKeyRequest
{
    public string? Name { get; set; }
    public string? ApiKey { get; set; }
    public bool? IsEnabled { get; set; }
    public bool? IsDefault { get; set; }
    public IReadOnlyList<string>? Models { get; set; }
}
