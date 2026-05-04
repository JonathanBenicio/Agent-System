using AgenticSystem.Core.LLM.Models;

namespace AgenticSystem.Core.LLM.Interfaces;

public interface ILLMManager
{
    Task<LLMResponse> GenerateAsync(LLMRequest request, CancellationToken ct = default);
    Task<LLMResponse> GenerateWithProfileAsync(string agentName, string taskType, string prompt, CancellationToken ct = default);
    ILLMProvider GetProvider(string name);
    ILLMProvider GetDefaultProvider();
    IEnumerable<ILLMProvider> GetEnabledProviders();
    IEnumerable<LLMProviderInfo> GetAllProviderInfo();
    Task<bool> TestProviderAsync(string name, CancellationToken ct = default);
    bool UpdateProvider(string name, UpdateProviderRequest request);
}

public class LLMProviderInfo
{
    public string Name { get; set; } = string.Empty;
    public string DefaultModel { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public int Priority { get; set; }
    public bool HasApiKey { get; set; }
}

public class UpdateProviderRequest
{
    public string? ApiKey { get; set; }
    public string? DefaultModel { get; set; }
    public bool? Enabled { get; set; }
    public int? Priority { get; set; }
}
