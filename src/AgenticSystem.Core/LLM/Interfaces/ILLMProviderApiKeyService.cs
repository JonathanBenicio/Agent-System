using AgenticSystem.Core.LLM.Models;

namespace AgenticSystem.Core.LLM.Interfaces;

public interface ILLMProviderApiKeyService
{
    Task<IReadOnlyList<LLMProviderApiKey>> GetKeysByProviderAsync(string providerName, CancellationToken ct = default);
    Task<LLMProviderApiKey> RegisterKeyAsync(string providerName, RegisterApiKeyRequest request, CancellationToken ct = default);
    Task<LLMProviderApiKey> UpdateKeyAsync(string providerName, string id, UpdateApiKeyRequest request, CancellationToken ct = default);
    Task DeleteKeyAsync(string providerName, string id, CancellationToken ct = default);
    Task SetDefaultKeyAsync(string providerName, string id, CancellationToken ct = default);
    Task<string> GetDecryptedKeyAsync(string providerName, string id, CancellationToken ct = default);
}
