using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;
using AgenticSystem.Api.Hubs;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Core.LLM.Interfaces;
using AgenticSystem.Infrastructure.Persistence;
using AgenticSystem.Infrastructure.Configuration;

namespace AgenticSystem.Api.Controllers;

[ApiController]
[Route("api/auth")]
[AllowAnonymous]
public class AuthController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TenantContext _tenantContext;
    private readonly AgenticDbContext _dbContext;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IConfiguration configuration,
        IServiceScopeFactory scopeFactory,
        TenantContext tenantContext,
        AgenticDbContext dbContext,
        ILogger<AuthController> logger)
    {
        _configuration = configuration;
        _scopeFactory = scopeFactory;
        _tenantContext = tenantContext;
        _dbContext = dbContext;
        _logger = logger;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ApiKey))
        {
            return BadRequest(new { error = "A chave de API é obrigatória." });
        }

        // 1. Calcula o Hash SHA-256 da chave fornecida
        var keyBytes = Encoding.UTF8.GetBytes(request.ApiKey.Trim());
        var hashBytes = SHA256.HashData(keyBytes);
        var keyHash = Convert.ToHexString(hashBytes).ToLowerInvariant();

        // 2. Consulta no banco de dados se o hash corresponde a uma chave ativa
        var accessKey = await _dbContext.AccessApiKeys
            .IgnoreQueryFilters() // Ignora o filtro de tenant no login
            .FirstOrDefaultAsync(k => k.KeyHash == keyHash && k.IsEnabled);

        if (accessKey is null)
        {
            return Unauthorized(new { error = "Chave de API inválida." });
        }

        // 3. Atualiza o timestamp de último uso
        try
        {
            accessKey.LastUsedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();
        }
        catch { /* Abafa erros de gravação de estatística */ }

        Response.Cookies.Append("agentic_api_key", request.ApiKey.Trim(), new CookieOptions
        {
            HttpOnly = true,
            Secure = true, // Em dev ou prod, garante envio seguro
            SameSite = SameSiteMode.Strict,
            Path = "/"
        });

        // Trigger LLM model discovery and update in the background asynchronously
        var tenantId = accessKey.TenantId;
        var scopeFactory = _scopeFactory;
        var logger = _logger;

        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<AgenticDbContext>();
                var encryptionService = scope.ServiceProvider.GetRequiredService<IConfigEncryptionService>();
                var llmAdminService = scope.ServiceProvider.GetRequiredService<ILLMAdministrationService>();

                var settings = scope.ServiceProvider.GetRequiredService<IOptions<AgenticSystemSettings>>().Value;
                var chatHubContext = scope.ServiceProvider.GetRequiredService<IHubContext<ChatHub>>();

                // Configure TenantContext in background thread scope to enforce tenant isolation in EF Core
                var tenantContext = scope.ServiceProvider.GetRequiredService<TenantContext>();
                tenantContext.TenantId = tenantId;

                logger.LogInformation("Background LLM model discovery started for tenant '{TenantId}' after successful admin login.", tenantId);

                bool anyModelUpdated = false;

                // 1. Discover models for enabled global infrastructure providers
                var globalProviders = new List<(string ProviderName, string ApiKey, bool Enabled)>
                {
                    ("OpenAI", settings.OpenAI.ApiKey, settings.OpenAI.Enabled),
                    ("Gemini", settings.Gemini.ApiKey, settings.Gemini.Enabled),
                    ("Claude", settings.Claude.ApiKey, settings.Claude.Enabled),
                    ("OpenRouter", settings.OpenRouter.ApiKey, settings.OpenRouter.Enabled)
                };

                foreach (var prov in globalProviders)
                {
                    if (prov.Enabled && !string.IsNullOrWhiteSpace(prov.ApiKey))
                    {
                        try
                        {
                            logger.LogInformation("Discovering models for global provider '{Provider}' (Tenant: {TenantId})...", prov.ProviderName, tenantId);
                            var response = await llmAdminService.DiscoverModelsAsync(
                                prov.ProviderName, 
                                new DiscoverModelsRequest { ApiKey = prov.ApiKey });

                            if (response.Success && response.DiscoveredModels != null && response.DiscoveredModels.Count > 0)
                            {
                                logger.LogInformation("Successfully discovered {Count} models for global provider '{Provider}'. Updating catalog...", response.DiscoveredModels.Count, prov.ProviderName);
                                await llmAdminService.UpdateProviderAsync(
                                    prov.ProviderName, 
                                    new UpdateProviderRequest 
                                    { 
                                        DiscoveredModels = response.DiscoveredModels, 
                                        ApiKey = prov.ApiKey, 
                                        Enabled = true 
                                    });
                                anyModelUpdated = true;
                            }
                            else
                            {
                                logger.LogWarning("Failed to discover models for global provider '{Provider}': {Error}", prov.ProviderName, response.ErrorMessage);
                            }
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "Error discovering models for global provider '{Provider}'", prov.ProviderName);
                        }
                    }
                }

                // 2. Discover models for database stored keys belonging strictly to this tenant
                try
                {
                    logger.LogInformation("Querying active stored provider keys for tenant '{TenantId}'...", tenantId);
                    var dbKeys = await dbContext.ProviderApiKeys
                        .Where(k => k.IsEnabled)
                        .ToListAsync();

                    logger.LogInformation("Found {Count} active stored keys for tenant '{TenantId}' in the database.", dbKeys.Count, tenantId);

                    foreach (var key in dbKeys)
                    {
                        try
                        {
                            logger.LogInformation("Decrypting key and discovering models for stored key '{KeyName}' ({Provider})...", key.Name, key.ProviderName);
                            var decryptedKey = encryptionService.Decrypt(key.EncryptedValue);
                            
                            var response = await llmAdminService.DiscoverModelsAsync(
                                key.ProviderName, 
                                new DiscoverModelsRequest { ApiKey = decryptedKey });

                            if (response.Success && response.DiscoveredModels != null && response.DiscoveredModels.Count > 0)
                            {
                                logger.LogInformation("Successfully discovered {Count} models for key '{KeyName}' ({Provider}). Updating database...", response.DiscoveredModels.Count, key.Name, key.ProviderName);
                                key.Models = string.Join(",", response.DiscoveredModels);
                                key.UpdatedAt = DateTime.UtcNow;
                                anyModelUpdated = true;
                            }
                            else
                            {
                                logger.LogWarning("Failed to discover models for stored key '{KeyName}' ({Provider}): {Error}", key.Name, key.ProviderName, response.ErrorMessage);
                            }
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "Error processing stored key '{KeyName}' ({Provider})", key.Name, key.ProviderName);
                        }
                    }

                    if (dbContext.ChangeTracker.HasChanges())
                    {
                        await dbContext.SaveChangesAsync();
                        logger.LogInformation("Successfully persisted updated model lists for active stored keys in the database for tenant '{TenantId}'.", tenantId);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error reading or updating stored API keys in the database for tenant '{TenantId}'", tenantId);
                }

                // 3. Notify frontend via SignalR Hub if any model catalog got updated
                if (anyModelUpdated)
                {
                    logger.LogInformation("Sending 'LlmCatalogUpdated' real-time notification to SignalR group 'tenant:{TenantId}'", tenantId);
                    await chatHubContext.Clients.Group($"tenant:{tenantId}").SendAsync("LlmCatalogUpdated");
                }

                logger.LogInformation("Background LLM model discovery completed for tenant '{TenantId}'.", tenantId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Fatal error running background LLM model discovery for tenant '{TenantId}'", tenantId);
            }
        });

        return Ok(new { success = true, role = "Admin", tenantId = accessKey.TenantId });
    }

    [HttpPost("logout")]
    public IActionResult Logout()
    {
        Response.Cookies.Delete("agentic_api_key", new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/"
        });

        return Ok(new { success = true });
    }
}

public record LoginRequest(string ApiKey);
