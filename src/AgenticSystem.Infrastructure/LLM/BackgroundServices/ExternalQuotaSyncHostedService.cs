using AgenticSystem.Core.Interfaces;
using AgenticSystem.Infrastructure.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgenticSystem.Infrastructure.LLM.BackgroundServices;

/// <summary>
/// Background service that proactively synchronizes external provider quotas (billing/balance).
/// </summary>
public class ExternalQuotaSyncHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<AgenticSystemSettings> _settings;
    private readonly ILogger<ExternalQuotaSyncHostedService> _logger;
    private readonly TimeSpan _syncInterval = TimeSpan.FromHours(1);

    public ExternalQuotaSyncHostedService(
        IServiceScopeFactory scopeFactory,
        IOptions<AgenticSystemSettings> settings,
        ILogger<ExternalQuotaSyncHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _settings = settings;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("External Quota Sync Hosted Service starting...");

        // Initial delay to let the system stabilize
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SyncAllQuotasAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during external quota synchronization");
            }

            await Task.Delay(_syncInterval, stoppingToken);
        }
    }

    private async Task SyncAllQuotasAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var syncService = scope.ServiceProvider.GetRequiredService<IExternalQuotaSyncService>();
        var tenantStore = scope.ServiceProvider.GetRequiredService<ITenantStore>();

        _logger.LogDebug("Starting proactive quota synchronization cycle");

        // 1. Sync Global Keys
        await SyncGlobalKeysAsync(syncService, ct);

        // 2. Sync Tenant Keys (BYOK)
        try 
        {
            var tenants = await tenantStore.GetAllAsync(ct);
            foreach (var tenant in tenants)
            {
                await SyncTenantKeysAsync(syncService, tenant, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve tenants for quota sync");
        }
        
        _logger.LogDebug("Quota synchronization cycle completed");
    }

    private async Task SyncGlobalKeysAsync(IExternalQuotaSyncService syncService, CancellationToken ct)
    {
        var settings = _settings.Value;

        if (settings.OpenAI.Enabled && !string.IsNullOrEmpty(settings.OpenAI.ApiKey))
        {
            await syncService.SyncBillingAsync("OpenAI", null, "global_openai", settings.OpenAI.ApiKey);
        }

        if (settings.OpenRouter.Enabled && !string.IsNullOrEmpty(settings.OpenRouter.ApiKey))
        {
            await syncService.SyncBillingAsync("OpenRouter", null, "global_openrouter", settings.OpenRouter.ApiKey);
        }

        if (settings.Claude.Enabled && !string.IsNullOrEmpty(settings.Claude.ApiKey))
        {
            await syncService.SyncBillingAsync("Claude", null, "global_claude", settings.Claude.ApiKey);
        }

        if (settings.Gemini.Enabled && !string.IsNullOrEmpty(settings.Gemini.ApiKey))
        {
            await syncService.SyncBillingAsync("Gemini", null, "global_gemini", settings.Gemini.ApiKey);
        }
    }

    private async Task SyncTenantKeysAsync(IExternalQuotaSyncService syncService, Core.Models.Tenant tenant, CancellationToken ct)
    {
        foreach (var providerKey in tenant.ProviderApiKeys)
        {
            var provider = providerKey.Key;
            var apiKey = providerKey.Value;

            if (string.IsNullOrEmpty(apiKey)) continue;

            // Only sync providers that support proactive billing
            if (provider.Equals("OpenAI", StringComparison.OrdinalIgnoreCase) || 
                provider.Equals("OpenRouter", StringComparison.OrdinalIgnoreCase))
            {
                await syncService.SyncBillingAsync(provider, tenant.Id, $"tenant_{tenant.Id}_{provider}", apiKey);
            }
        }
    }
}
