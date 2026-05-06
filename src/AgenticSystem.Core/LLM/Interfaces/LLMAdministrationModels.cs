namespace AgenticSystem.Core.LLM.Interfaces;

public class LLMProviderInfo
{
    public string Name { get; set; } = string.Empty;
    public string DefaultModel { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public int Priority { get; set; }
    public bool HasApiKey { get; set; }
    public bool IsDefault { get; set; }
    public bool IsAvailable { get; set; }
    public IReadOnlyList<string> Models { get; set; } = [];
}

public class LLMConfigurationInfo
{
    public string DefaultProvider { get; set; } = string.Empty;
    public string DefaultModel { get; set; } = string.Empty;
    public IReadOnlyList<LLMProviderInfo> Providers { get; set; } = [];
}

public class UpdateProviderRequest
{
    public string? ApiKey { get; set; }
    public string? DefaultModel { get; set; }
    public bool? Enabled { get; set; }
    public int? Priority { get; set; }
}

public class UpdateDefaultLlmSelectionRequest
{
    public string ProviderName { get; set; } = string.Empty;
    public string? Model { get; set; }
}