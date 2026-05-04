namespace AgenticSystem.Core.LLM.Models;

// ═══════════════════════════════════════════════════════════
// Vision Models
// ═══════════════════════════════════════════════════════════

public class VisionRequest
{
    public string ImageUrl { get; set; } = string.Empty;
    public byte[]? ImageBytes { get; set; }
    public string Prompt { get; set; } = "Describe this image.";
    public string? Model { get; set; }
    public int MaxTokens { get; set; } = 1000;
}

public class VisionResponse
{
    public string Description { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public bool Success { get; set; } = true;
    public string? ErrorMessage { get; set; }
    public UsageInfo Usage { get; set; } = new();

    public static VisionResponse Ok(string description, string model, string provider)
        => new() { Description = description, Model = model, Provider = provider, Success = true };

    public static VisionResponse Fail(string error, string provider)
        => new() { Success = false, ErrorMessage = error, Provider = provider };
}
