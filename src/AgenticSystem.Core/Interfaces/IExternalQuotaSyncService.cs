using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Interfaces;

/// <summary>
/// Service responsible for syncing and updating external provider quotas.
/// </summary>
public interface IExternalQuotaSyncService
{
    /// <summary>
    /// Updates the quota based on headers captured reactively.
    /// </summary>
    Task UpdateFromHeadersAsync(string providerName, ExternalQuotaOwner owner, string apiKeyId, long limitRequests, long remainingRequests, long limitTokens, long remainingTokens, DateTime? resetAt);

    /// <summary>
    /// Proactively syncs billing/balance for a specific provider.
    /// </summary>
    Task SyncBillingAsync(string providerName, ExternalQuotaOwner owner, string apiKeyId, string apiKey);

    /// <summary>
    /// Gets the current quota status for a provider and key.
    /// </summary>
    Task<ExternalProviderQuota?> GetQuotaAsync(string providerName, ExternalQuotaOwner owner, string apiKeyId);

    /// <summary>
    /// Checks if a provider has available quota/balance.
    /// </summary>
    Task<bool> HasAvailableQuotaAsync(string providerName, ExternalQuotaOwner owner, string apiKeyId);

    /// <summary>
    /// Simplified check: returns true if the provider has ANY available key (Global or Tenant).
    /// </summary>
    Task<bool> IsProviderAvailableAsync(string providerName, ExternalQuotaOwner owner);

    /// <summary>
    /// Gets all current quotas for a tenant (or global).
    /// </summary>
    Task<IReadOnlyList<ExternalProviderQuota>> GetAllQuotasAsync(ExternalQuotaOwner owner);
}
