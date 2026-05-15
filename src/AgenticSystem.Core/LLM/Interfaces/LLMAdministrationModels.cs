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
    public double? CurrentBalance { get; set; }
    public long? RequestsRemaining { get; set; }
    public long? TokensRemaining { get; set; }
    public bool QuotaExceeded { get; set; }
    public DateTime? LastQuotaUpdate { get; set; }
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
    public IReadOnlyList<string>? DiscoveredModels { get; set; }
}

public class UpdateDefaultLlmSelectionRequest
{
    public string ProviderName { get; set; } = string.Empty;
    public string? Model { get; set; }
}

public class DiscoverModelsRequest
{
    public string ApiKey { get; set; } = string.Empty;
}

public class DiscoverModelsResponse
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public IReadOnlyList<string> DiscoveredModels { get; set; } = [];
}