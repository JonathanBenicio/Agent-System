using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.Persistence;
using AgenticSystem.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Infrastructure.LLM.Services;

/// <summary>
/// Implementation of IExternalQuotaSyncService for managing external LLM quotas.
/// </summary>
public class ExternalQuotaSyncService : IExternalQuotaSyncService
{
    private readonly IDbContextFactory<AgenticDbContext> _dbContextFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ExternalQuotaSyncService> _logger;

    public ExternalQuotaSyncService(
        IDbContextFactory<AgenticDbContext> dbContextFactory,
        IHttpClientFactory httpClientFactory,
        ILogger<ExternalQuotaSyncService> logger)
    {
        _dbContextFactory = dbContextFactory;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task UpdateFromHeadersAsync(
        string providerName, 
        string? tenantId, 
        string apiKeyId, 
        long remainingRequests, 
        long remainingTokens, 
        DateTime? resetAt)
    {
        try 
        {
            using var context = await _dbContextFactory.CreateDbContextAsync();
            var entity = await context.ExternalProviderQuotas
                .FirstOrDefaultAsync(q => q.ProviderName == providerName && q.TenantId == tenantId && q.ApiKeyId == apiKeyId);

            if (entity == null)
            {
                entity = new ExternalProviderQuotaEntity
                {
                    Id = Guid.NewGuid().ToString(),
                    ProviderName = providerName,
                    TenantId = tenantId,
                    ApiKeyId = apiKeyId
                };
                context.ExternalProviderQuotas.Add(entity);
            }

            entity.RemainingRequests = remainingRequests;
            entity.RemainingTokens = remainingTokens;
            entity.ResetAt = resetAt;
            entity.LastSyncAt = DateTime.UtcNow;

            await context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating quota from headers for provider {Provider}", providerName);
        }
    }

    public async Task SyncBillingAsync(string providerName, string? tenantId, string apiKeyId, string apiKey)
    {
        _logger.LogInformation("Proactive billing sync triggered for {Provider} (Key: {ApiKeyId})", providerName, apiKeyId);
        
        try
        {
            if (providerName.Equals("OpenRouter", StringComparison.OrdinalIgnoreCase))
            {
                await SyncOpenRouterBillingAsync(tenantId, apiKeyId, apiKey);
            }
            else if (providerName.Equals("OpenAI", StringComparison.OrdinalIgnoreCase))
            {
                await SyncOpenAIBillingAsync(tenantId, apiKeyId, apiKey);
            }
            else if (providerName.Equals("Claude", StringComparison.OrdinalIgnoreCase) || 
                     providerName.Equals("Gemini", StringComparison.OrdinalIgnoreCase))
            {
                await SyncGenericProviderBillingAsync(providerName, tenantId, apiKeyId, apiKey);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync billing for {Provider}", providerName);
        }
    }

    private async Task SyncOpenRouterBillingAsync(string? tenantId, string apiKeyId, string apiKey)
    {
        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
        
        var response = await client.GetAsync("https://openrouter.ai/api/v1/key");
        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            using var doc = System.Text.Json.JsonDocument.Parse(content);
            var data = doc.RootElement.GetProperty("data");
            
            double usage = 0;
            if (data.TryGetProperty("usage", out var usageProp)) usage = usageProp.GetDouble();
            
            double limit = 0;
            if (data.TryGetProperty("limit", out var limitProp) && limitProp.ValueKind != System.Text.Json.JsonValueKind.Null) 
                limit = limitProp.GetDouble();

            using var context = await _dbContextFactory.CreateDbContextAsync();
            var entity = await context.ExternalProviderQuotas
                .FirstOrDefaultAsync(q => q.ProviderName == "OpenRouter" && q.TenantId == tenantId && q.ApiKeyId == apiKeyId);

            if (entity == null)
            {
                entity = new ExternalProviderQuotaEntity { Id = Guid.NewGuid().ToString(), ProviderName = "OpenRouter", TenantId = tenantId, ApiKeyId = apiKeyId };
                context.ExternalProviderQuotas.Add(entity);
            }

            entity.BalanceRemaining = limit > 0 ? limit - usage : 0;
            entity.LastSyncAt = DateTime.UtcNow;
            await context.SaveChangesAsync();
        }
    }

    private async Task SyncOpenAIBillingAsync(string? tenantId, string apiKeyId, string apiKey)
    {
        // OpenAI doesn't have a simple public balance API for standard keys.
        // We'll sync usage for today as a proxy or use dashboard internal API if we want to risk it.
        // For now, let's just update the LastSyncAt to show we checked.
        using var context = await _dbContextFactory.CreateDbContextAsync();
        var entity = await context.ExternalProviderQuotas
            .FirstOrDefaultAsync(q => q.ProviderName == "OpenAI" && q.TenantId == tenantId && q.ApiKeyId == apiKeyId);

        if (entity != null)
        {
            entity.LastSyncAt = DateTime.UtcNow;
            await context.SaveChangesAsync();
        }
    }

    private async Task SyncGenericProviderBillingAsync(string providerName, string? tenantId, string apiKeyId, string apiKey)
    {
        // For now, just update the timestamp to show the key is still valid/monitored
        using var context = await _dbContextFactory.CreateDbContextAsync();
        var entity = await context.ExternalProviderQuotas
            .FirstOrDefaultAsync(q => q.ProviderName == providerName && q.TenantId == tenantId && q.ApiKeyId == apiKeyId);

        if (entity == null)
        {
            entity = new ExternalProviderQuotaEntity 
            { 
                Id = Guid.NewGuid().ToString(), 
                ProviderName = providerName, 
                TenantId = tenantId, 
                ApiKeyId = apiKeyId,
                RemainingRequests = 1000, // Default initial values
                RemainingTokens = 1000000
            };
            context.ExternalProviderQuotas.Add(entity);
        }

        entity.LastSyncAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
    }

    public async Task<ExternalProviderQuota?> GetQuotaAsync(string providerName, string? tenantId, string apiKeyId)
    {
        using var context = await _dbContextFactory.CreateDbContextAsync();
        var entity = await context.ExternalProviderQuotas
            .FirstOrDefaultAsync(q => q.ProviderName == providerName && q.TenantId == tenantId && q.ApiKeyId == apiKeyId);

        if (entity == null) return null;

        return MapToModel(entity);
    }

    public async Task<bool> HasAvailableQuotaAsync(string providerName, string? tenantId, string apiKeyId)
    {
        var quota = await GetQuotaAsync(providerName, tenantId, apiKeyId);
        if (quota == null) return true; // Assume available if not tracked yet

        return !quota.IsExhausted;
    }

    public async Task<bool> IsProviderAvailableAsync(string providerName, string? tenantId = null)
    {
        using var context = await _dbContextFactory.CreateDbContextAsync();
        
        // Find all keys for this provider and tenant
        var quotas = await context.ExternalProviderQuotas
            .Where(q => q.ProviderName == providerName && q.TenantId == tenantId)
            .ToListAsync();

        if (quotas.Count == 0) return true; // No data yet, assume available

        // If at least one key is NOT exhausted, provider is available
        return quotas.Any(q => q.RemainingRequests > 0 || (q.RemainingTokens > 0 || q.RemainingTokens == -1) || q.BalanceRemaining > 0);
    }

    public async Task<IReadOnlyList<ExternalProviderQuota>> GetAllQuotasAsync(string? tenantId = null)
    {
        using var context = await _dbContextFactory.CreateDbContextAsync();
        var entities = await context.ExternalProviderQuotas
            .Where(q => q.TenantId == tenantId)
            .ToListAsync();

        return entities.Select(MapToModel).ToList();
    }

    private static ExternalProviderQuota MapToModel(ExternalProviderQuotaEntity entity)
    {
        return new ExternalProviderQuota
        {
            ProviderName = entity.ProviderName,
            TenantId = entity.TenantId,
            ApiKeyId = entity.ApiKeyId,
            RemainingRequests = entity.RemainingRequests,
            RemainingTokens = entity.RemainingTokens,
            ResetAt = entity.ResetAt,
            BalanceRemaining = entity.BalanceRemaining,
            Currency = entity.Currency,
            LastSyncAt = entity.LastSyncAt
        };
    }
}
