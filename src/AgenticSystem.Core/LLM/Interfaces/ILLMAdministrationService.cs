namespace AgenticSystem.Core.LLM.Interfaces;

public interface ILLMAdministrationService
{
    Task<LLMConfigurationInfo> GetConfigurationAsync(CancellationToken ct = default);
    Task<IReadOnlyList<LLMProviderInfo>> GetEnabledProvidersAsync(CancellationToken ct = default);
    Task<LLMProviderInfo?> GetProviderAsync(string name, CancellationToken ct = default);
    Task<LLMProviderInfo?> GetDefaultProviderAsync(CancellationToken ct = default);
    Task<bool> TestProviderAsync(string name, CancellationToken ct = default);
    Task<LLMConfigurationInfo> UpdateDefaultSelectionAsync(UpdateDefaultLlmSelectionRequest request, CancellationToken ct = default);
    Task<LLMProviderInfo?> UpdateProviderAsync(string name, UpdateProviderRequest request, CancellationToken ct = default);
    Task<DiscoverModelsResponse> DiscoverModelsAsync(string name, DiscoverModelsRequest request, CancellationToken ct = default);
    Task SyncQuotasAsync(CancellationToken ct = default);
}