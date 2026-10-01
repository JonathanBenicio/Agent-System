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
    private readonly IEventBus _eventBus;
    private readonly ILogger<ExternalQuotaSyncService> _logger;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly ISystemOperationContextAccessor _systemOperations;

    public ExternalQuotaSyncService(
        IDbContextFactory<AgenticDbContext> dbContextFactory,
        IHttpClientFactory httpClientFactory,
        IEventBus eventBus,
        ILogger<ExternalQuotaSyncService> logger,
        ITenantContextAccessor tenantAccessor,
        ISystemOperationContextAccessor systemOperations)
    {
        _dbContextFactory = dbContextFactory;
        _httpClientFactory = httpClientFactory;
        _eventBus = eventBus;
        _logger = logger;
        _tenantAccessor = tenantAccessor;
        _systemOperations = systemOperations;
    }

    public async Task UpdateFromHeadersAsync(
        string providerName, 
        ExternalQuotaOwner owner,
        string apiKeyId, 
        long limitRequests,
        long remainingRequests, 
        long limitTokens, 
        long remainingTokens, 
        DateTime? resetAt)
    {
        ArgumentNullException.ThrowIfNull(owner);
        await WithOwnerContextAsync(owner, async context =>
        {
            var entity = await FindQuotaRecordAsync(context, owner, providerName, apiKeyId);

            if (entity == null)
            {
                entity = CreateQuotaRecord(owner, providerName, apiKeyId);
                AddQuotaRecord(context, owner, entity);
            }

            entity.LimitRequests = limitRequests;
            entity.RemainingRequests = remainingRequests;
            entity.LimitTokens = limitTokens;
            entity.RemainingTokens = remainingTokens;
            entity.ResetAt = resetAt;
            entity.LastSyncAt = DateTime.UtcNow;

            await CheckCriticalThresholdsAsync(entity, owner, context);
            await context.SaveChangesAsync();
            return true;
        });
    }

    public async Task SyncBillingAsync(string providerName, ExternalQuotaOwner owner, string apiKeyId, string apiKey)
    {
        ArgumentNullException.ThrowIfNull(owner);
        _logger.LogInformation("Proactive billing sync triggered for {Provider} (Key: {ApiKeyId})", providerName, apiKeyId);

        try
        {
            if (providerName.Equals("OpenRouter", StringComparison.OrdinalIgnoreCase))
            {
                await SyncOpenRouterBillingAsync(owner, apiKeyId, apiKey);
            }
            else if (providerName.Equals("OpenAI", StringComparison.OrdinalIgnoreCase))
            {
                await SyncOpenAIBillingAsync(owner, apiKeyId, apiKey);
            }
            else if (providerName.Equals("Claude", StringComparison.OrdinalIgnoreCase) ||
                     providerName.Equals("Gemini", StringComparison.OrdinalIgnoreCase))
            {
                await SyncGenericProviderBillingAsync(providerName, owner, apiKeyId, apiKey);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync billing for {Provider}", providerName);
        }
    }

    private async Task SyncOpenRouterBillingAsync(ExternalQuotaOwner owner, string apiKeyId, string apiKey)
    {
        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

        var response = await client.GetAsync("https://openrouter.ai/api/v1/key");
        if (!response.IsSuccessStatusCode) return;

        var content = await response.Content.ReadAsStringAsync();
        using var doc = System.Text.Json.JsonDocument.Parse(content);
        var data = doc.RootElement.GetProperty("data");

        double usage = 0;
        if (data.TryGetProperty("usage", out var usageProp)) usage = usageProp.GetDouble();

        double limit = 0;
        if (data.TryGetProperty("limit", out var limitProp) && limitProp.ValueKind != System.Text.Json.JsonValueKind.Null)
            limit = limitProp.GetDouble();

        await WithOwnerContextAsync(owner, async context =>
        {
            var entity = await FindOrCreateQuotaRecordAsync(context, owner, "OpenRouter", apiKeyId);
            entity.BalanceRemaining = limit > 0 ? limit - usage : 0;
            entity.LastSyncAt = DateTime.UtcNow;
            await CheckCriticalThresholdsAsync(entity, owner, context);
            await context.SaveChangesAsync();
            return true;
        });
    }

    private async Task SyncOpenAIBillingAsync(ExternalQuotaOwner owner, string apiKeyId, string apiKey)
    {
        // OpenAI doesn't expose a public balance API for standard keys; record the successful check.
        await WithOwnerContextAsync(owner, async context =>
        {
            var entity = await FindQuotaRecordAsync(context, owner, "OpenAI", apiKeyId);
            if (entity is not null)
            {
                entity.LastSyncAt = DateTime.UtcNow;
                await context.SaveChangesAsync();
            }
            return true;
        });
    }

    private async Task SyncGenericProviderBillingAsync(string providerName, ExternalQuotaOwner owner, string apiKeyId, string apiKey)
    {
        await WithOwnerContextAsync(owner, async context =>
        {
            var entity = await FindOrCreateQuotaRecordAsync(context, owner, providerName, apiKeyId);
            if (entity.LimitRequests == 0 && entity.RemainingRequests == 0)
                entity.RemainingRequests = 1000;
            if (entity.LimitTokens == 0 && entity.RemainingTokens == 0)
                entity.RemainingTokens = 1000000;
            entity.LastSyncAt = DateTime.UtcNow;
            await context.SaveChangesAsync();
            return true;
        });
    }

    public async Task<ExternalProviderQuota?> GetQuotaAsync(string providerName, ExternalQuotaOwner owner, string apiKeyId)
    {
        ArgumentNullException.ThrowIfNull(owner);
        return await WithOwnerContextAsync(owner, async context =>
        {
            var entity = await FindQuotaRecordAsync(context, owner, providerName, apiKeyId);
            return entity is null ? null : MapToModel(entity, owner);
        });
    }

    public async Task<bool> HasAvailableQuotaAsync(string providerName, ExternalQuotaOwner owner, string apiKeyId)
    {
        var quota = await GetQuotaAsync(providerName, owner, apiKeyId);
        if (quota == null) return true;
        return !quota.IsExhausted;
    }

    public async Task<bool> IsProviderAvailableAsync(string providerName, ExternalQuotaOwner owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        return await WithOwnerContextAsync(owner, async context =>
        {
            if (owner.IsPlatform)
            {
                var quotas = await context.PlatformExternalProviderQuotas
                    .Where(q => q.ProviderName == providerName)
                    .Select(q => new { q.RemainingRequests, q.RemainingTokens, q.BalanceRemaining })
                    .ToListAsync();
                return quotas.Count == 0 || quotas.Any(q => q.RemainingRequests > 0 || q.RemainingTokens > 0 || q.RemainingTokens == -1 || q.BalanceRemaining > 0);
            }

            var tenantQuotas = await context.ExternalProviderQuotas
                .Where(q => q.ProviderName == providerName && q.TenantId == owner.TenantId)
                .Select(q => new { q.RemainingRequests, q.RemainingTokens, q.BalanceRemaining })
                .ToListAsync();
            return tenantQuotas.Count == 0 || tenantQuotas.Any(q => q.RemainingRequests > 0 || q.RemainingTokens > 0 || q.RemainingTokens == -1 || q.BalanceRemaining > 0);
        });
    }

    public async Task<IReadOnlyList<ExternalProviderQuota>> GetAllQuotasAsync(ExternalQuotaOwner owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        return await WithOwnerContextAsync(owner, async context =>
        {
            if (owner.IsPlatform)
            {
                var platformRows = await context.PlatformExternalProviderQuotas.AsNoTracking().ToListAsync();
                return (IReadOnlyList<ExternalProviderQuota>)platformRows.Select(row => MapToModel(row, owner)).ToList();
            }

            var tenantRows = await context.ExternalProviderQuotas.AsNoTracking()
                .Where(row => row.TenantId == owner.TenantId)
                .ToListAsync();
            return tenantRows.Select(row => MapToModel(row, owner)).ToList();
        });
    }

    private async Task<T> WithOwnerContextAsync<T>(
        ExternalQuotaOwner owner,
        Func<AgenticDbContext, Task<T>> action)
    {
        if (owner.IsPlatform)
        {
            using var systemScope = _systemOperations.BeginScope(SystemOperationKind.PlatformQuotaSync);
            _systemOperations.Require(SystemOperationKind.PlatformQuotaSync);
            await using var platformContext = await _dbContextFactory.CreateDbContextAsync();
            return await action(platformContext);
        }

        using var tenantScope = _tenantAccessor.BeginScope(new TenantContext { TenantId = owner.TenantId! });
        await using var tenantContext = await _dbContextFactory.CreateDbContextAsync();
        return await action(tenantContext);
    }

    private static async Task<IExternalProviderQuotaRecord?> FindQuotaRecordAsync(
        AgenticDbContext context,
        ExternalQuotaOwner owner,
        string providerName,
        string apiKeyId)
    {
        if (owner.IsPlatform)
        {
            return await context.PlatformExternalProviderQuotas
                .FirstOrDefaultAsync(quota => quota.ProviderName == providerName && quota.ApiKeyId == apiKeyId);
        }

        return await context.ExternalProviderQuotas
            .FirstOrDefaultAsync(quota => quota.ProviderName == providerName && quota.TenantId == owner.TenantId && quota.ApiKeyId == apiKeyId);
    }

    private static async Task<IExternalProviderQuotaRecord> FindOrCreateQuotaRecordAsync(
        AgenticDbContext context,
        ExternalQuotaOwner owner,
        string providerName,
        string apiKeyId)
    {
        var entity = await FindQuotaRecordAsync(context, owner, providerName, apiKeyId);
        if (entity is not null) return entity;

        entity = CreateQuotaRecord(owner, providerName, apiKeyId);
        AddQuotaRecord(context, owner, entity);
        return entity;
    }

    private static IExternalProviderQuotaRecord CreateQuotaRecord(ExternalQuotaOwner owner, string providerName, string apiKeyId) =>
        owner.IsPlatform
            ? new PlatformExternalProviderQuotaEntity { Id = Guid.NewGuid().ToString(), ProviderName = providerName, ApiKeyId = apiKeyId }
            : new ExternalProviderQuotaEntity { Id = Guid.NewGuid().ToString(), ProviderName = providerName, TenantId = owner.TenantId!, ApiKeyId = apiKeyId };

    private static void AddQuotaRecord(AgenticDbContext context, ExternalQuotaOwner owner, IExternalProviderQuotaRecord entity)
    {
        if (owner.IsPlatform)
            context.PlatformExternalProviderQuotas.Add((PlatformExternalProviderQuotaEntity)entity);
        else
            context.ExternalProviderQuotas.Add((ExternalProviderQuotaEntity)entity);
    }

    private static ExternalProviderQuota MapToModel(IExternalProviderQuotaRecord entity, ExternalQuotaOwner owner)
    {
        var model = new ExternalProviderQuota
        {
            ProviderName = entity.ProviderName,
            TenantId = owner.TenantId,
            ApiKeyId = entity.ApiKeyId,
            LimitRequests = entity.LimitRequests,
            RemainingRequests = entity.RemainingRequests,
            LimitTokens = entity.LimitTokens,
            RemainingTokens = entity.RemainingTokens,
            ResetAt = entity.ResetAt,
            TotalBalance = entity.TotalBalance,
            BalanceRemaining = entity.BalanceRemaining,
            Currency = entity.Currency,
            LastSyncAt = entity.LastSyncAt
        };
        return model;
    }

    private async Task CheckCriticalThresholdsAsync(IExternalProviderQuotaRecord entity, ExternalQuotaOwner owner, AgenticDbContext context)
    {
        // 10% Threshold check
        if (entity.LimitRequests > 0 && (double)entity.RemainingRequests / entity.LimitRequests < 0.1)
        {
            var percentage = (double)entity.RemainingRequests / entity.LimitRequests * 100;
            _logger.LogCritical("🚨 CRITICAL QUOTA ALERT: Provider {Provider} (Key: {ApiKeyId}) is below 10% requests remaining ({Remaining}/{Limit})",
                entity.ProviderName, entity.ApiKeyId, entity.RemainingRequests, entity.LimitRequests);

            await PublishQuotaAlertAsync(owner, new Dictionary<string, object>
            {
                ["ProviderName"] = entity.ProviderName,
                ["ApiKeyId"] = entity.ApiKeyId,
                ["Type"] = "Requests",
                ["Remaining"] = entity.RemainingRequests,
                ["Limit"] = entity.LimitRequests,
                ["Percentage"] = percentage
            });

            await RecordAlertIfMissingAsync(owner, context, "Requests",
                $"Provider {entity.ProviderName} is below 10% requests remaining.", entity.ProviderName, percentage);
        }

        if (entity.LimitTokens > 0 && (double)entity.RemainingTokens / entity.LimitTokens < 0.1)
        {
            var percentage = (double)entity.RemainingTokens / entity.LimitTokens * 100;
            _logger.LogCritical("🚨 CRITICAL QUOTA ALERT: Provider {Provider} (Key: {ApiKeyId}) is below 10% tokens remaining ({Remaining}/{Limit})",
                entity.ProviderName, entity.ApiKeyId, entity.RemainingTokens, entity.LimitTokens);

            await PublishQuotaAlertAsync(owner, new Dictionary<string, object>
            {
                ["ProviderName"] = entity.ProviderName,
                ["ApiKeyId"] = entity.ApiKeyId,
                ["Type"] = "Tokens",
                ["Remaining"] = entity.RemainingTokens,
                ["Limit"] = entity.LimitTokens,
                ["Percentage"] = percentage
            });

            await RecordAlertIfMissingAsync(owner, context, "Tokens",
                $"Provider {entity.ProviderName} is below 10% tokens remaining.", entity.ProviderName, percentage);
        }

        if (entity.TotalBalance > 0 && entity.BalanceRemaining / entity.TotalBalance < 0.1)
        {
            var percentage = entity.BalanceRemaining / entity.TotalBalance * 100;
            _logger.LogCritical("🚨 CRITICAL BILLING ALERT: Provider {Provider} (Key: {ApiKeyId}) is below 10% balance remaining ({Remaining:F2}/{Total:F2} {Currency})",
                entity.ProviderName, entity.ApiKeyId, entity.BalanceRemaining, entity.TotalBalance, entity.Currency);

            await PublishQuotaAlertAsync(owner, new Dictionary<string, object>
            {
                ["ProviderName"] = entity.ProviderName,
                ["ApiKeyId"] = entity.ApiKeyId,
                ["Type"] = "Balance",
                ["Remaining"] = entity.BalanceRemaining,
                ["Limit"] = entity.TotalBalance,
                ["Currency"] = entity.Currency,
                ["Percentage"] = percentage
            });

            await RecordAlertIfMissingAsync(owner, context, "Balance",
                $"Provider {entity.ProviderName} balance is critically low.", entity.ProviderName, percentage);
        }
    }

    private static async Task RecordAlertIfMissingAsync(
        ExternalQuotaOwner owner,
        AgenticDbContext context,
        string type,
        string message,
        string providerName,
        double percentage)
    {
        var oneHourAgo = DateTime.UtcNow.AddHours(-1);
        var exists = owner.IsPlatform
            ? await context.SystemAlerts.AnyAsync(alert =>
                alert.ProviderName == providerName && alert.Type == type && alert.CreatedAt > oneHourAgo)
            : await context.TenantSystemAlerts.AnyAsync(alert =>
                alert.ProviderName == providerName && alert.Type == type && alert.CreatedAt > oneHourAgo);
        if (exists) return;

        if (owner.IsPlatform)
        {
            context.SystemAlerts.Add(new SystemAlertEntity
            {
                Id = Guid.NewGuid().ToString(),
                Type = type,
                Severity = "Critical",
                Message = message,
                ProviderName = providerName,
                Percentage = percentage,
                CreatedAt = DateTime.UtcNow,
                IsRead = false
            });
            return;
        }

        context.TenantSystemAlerts.Add(new TenantSystemAlertEntity
        {
            Id = Guid.NewGuid().ToString(),
            TenantId = owner.TenantId!,
            Type = type,
            Severity = "Critical",
            Message = message,
            ProviderName = providerName,
            Percentage = percentage,
            CreatedAt = DateTime.UtcNow,
            IsRead = false
        });
    }

    private async Task PublishQuotaAlertAsync(ExternalQuotaOwner owner, Dictionary<string, object> payload)
    {
        if (owner.IsPlatform)
        {
            using var systemScope = _systemOperations.BeginScope(SystemOperationKind.PublishPlatformEvent);
            _systemOperations.Require(SystemOperationKind.PublishPlatformEvent);
            await _eventBus.PublishAsync(new SystemBusEvent
            {
                EventType = "FinOps.QuotaThresholdReached",
                Source = "QuotaSyncService",
                TenantId = null,
                Payload = payload
            }).ConfigureAwait(false);
            return;
        }

        await _eventBus.PublishAsync(new SystemBusEvent
        {
            EventType = "FinOps.QuotaThresholdReached",
            Source = "QuotaSyncService",
            TenantId = owner.TenantId,
            Payload = payload
        }).ConfigureAwait(false);
    }
}
