using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using System.Collections.Concurrent;

namespace AgenticSystem.Core.Services;

/// <summary>
/// In-memory fallback for IExternalQuotaSyncService when PostgreSQL is not configured.
/// </summary>
public class InMemoryExternalQuotaSyncService : IExternalQuotaSyncService
{
    private readonly ConcurrentDictionary<string, ExternalProviderQuota> _quotas = new();

    private static string GetKey(string providerName, ExternalQuotaOwner owner, string apiKeyId) =>
        $"{providerName}:{owner.TenantId ?? "<platform>"}:{apiKeyId}";

    public Task UpdateFromHeadersAsync(
        string providerName, 
        ExternalQuotaOwner owner,
        string apiKeyId, 
        long limitRequests, 
        long remainingRequests, 
        long limitTokens, 
        long remainingTokens, 
        DateTime? resetAt)
    {
        var key = GetKey(providerName, owner, apiKeyId);
        var quota = new ExternalProviderQuota
        {
            ProviderName = providerName,
            TenantId = owner.TenantId,
            ApiKeyId = apiKeyId,
            LimitRequests = limitRequests,
            RemainingRequests = remainingRequests,
            LimitTokens = limitTokens,
            RemainingTokens = remainingTokens,
            ResetAt = resetAt,
            LastSyncAt = DateTime.UtcNow
        };

        _quotas[key] = quota;
        return Task.CompletedTask;
    }

    public Task SyncBillingAsync(string providerName, ExternalQuotaOwner owner, string apiKeyId, string apiKey)
    {
        // Fake sync
        var key = GetKey(providerName, owner, apiKeyId);
        if (!_quotas.TryGetValue(key, out var quota))
        {
            quota = new ExternalProviderQuota
            {
                ProviderName = providerName,
                TenantId = owner.TenantId,
                ApiKeyId = apiKeyId,
                RemainingRequests = 1000,
                RemainingTokens = 1000000,
                LastSyncAt = DateTime.UtcNow
            };
            _quotas[key] = quota;
        }
        else
        {
            quota.LastSyncAt = DateTime.UtcNow;
        }

        return Task.CompletedTask;
    }

    public Task<ExternalProviderQuota?> GetQuotaAsync(string providerName, ExternalQuotaOwner owner, string apiKeyId)
    {
        var key = GetKey(providerName, owner, apiKeyId);
        _quotas.TryGetValue(key, out var quota);
        return Task.FromResult(quota);
    }

    public Task<bool> HasAvailableQuotaAsync(string providerName, ExternalQuotaOwner owner, string apiKeyId)
    {
        var key = GetKey(providerName, owner, apiKeyId);
        if (!_quotas.TryGetValue(key, out var quota))
        {
            return Task.FromResult(true); // Assume available if not tracked
        }

        return Task.FromResult(!quota.IsExhausted);
    }

    public Task<bool> IsProviderAvailableAsync(string providerName, ExternalQuotaOwner owner)
    {
        var prefix = $"{providerName}:{owner.TenantId ?? "<platform>"}";
        var keys = _quotas.Keys.Where(k => k.StartsWith(prefix));

        if (!keys.Any())
        {
            return Task.FromResult(true); // No data, assume available
        }

        var available = keys.Any(k => !_quotas[k].IsExhausted);
        return Task.FromResult(available);
    }

    public Task<IReadOnlyList<ExternalProviderQuota>> GetAllQuotasAsync(ExternalQuotaOwner owner)
    {
        var result = _quotas.Values
            .Where(q => q.TenantId == owner.TenantId)
            .ToList();

        return Task.FromResult<IReadOnlyList<ExternalProviderQuota>>(result);
    }
}
