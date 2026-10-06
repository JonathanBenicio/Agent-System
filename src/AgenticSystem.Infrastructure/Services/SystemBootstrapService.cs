using System.Security.Cryptography;
using System.Text;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.Persistence;
using AgenticSystem.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Infrastructure.Services;

/// <summary>
/// Creates the initial tenant only when an administrator API key is explicitly configured.
/// </summary>
public sealed class SystemBootstrapService : ISystemBootstrapService
{
    private readonly AgenticDbContext _dbContext;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SystemBootstrapService> _logger;
    private readonly ISystemOperationContextAccessor _systemOperations;
    private readonly ITenantContextAccessor _tenantContext;

    public SystemBootstrapService(
        AgenticDbContext dbContext,
        IConfiguration configuration,
        ILogger<SystemBootstrapService> logger,
        ISystemOperationContextAccessor systemOperations,
        ITenantContextAccessor tenantContext)
    {
        _dbContext = dbContext;
        _configuration = configuration;
        _logger = logger;
        _systemOperations = systemOperations;
        _tenantContext = tenantContext;
    }

    public async Task BootstrapAsync(CancellationToken cancellationToken = default)
    {
        using var systemScope = _systemOperations.BeginScope(SystemOperationKind.Bootstrap);
        _systemOperations.Require(SystemOperationKind.Bootstrap);
        try
        {
            var tenantExists = await _dbContext.Tenants.IgnoreQueryFilters().AnyAsync(cancellationToken);
            var adminApiKey = _configuration["AgenticSystem:AdminApiKey"];

            if (!tenantExists && string.IsNullOrWhiteSpace(adminApiKey))
            {
                _logger.LogWarning("No tenant is provisioned and AgenticSystem:AdminApiKey is not configured. The API will start without tenant access until an administrator provisions one.");
            }
            else if (!tenantExists)
            {
                var tenant = new Tenant
                {
                    Id = "admin",
                    Name = "Administrator Tenant",
                    Slug = "admin",
                    Plan = TenantPlan.Pro,
                    Limits = TenantLimits.ProTier(),
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                var keyHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(adminApiKey!.Trim())))
                    .ToLowerInvariant();
                var accessKey = new AccessApiKeyEntity
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenant.Id,
                    KeyHash = keyHash,
                    Name = "Configured Bootstrap Admin API Key",
                    Role = "Admin",
                    IsEnabled = true,
                    CreatedAt = DateTime.UtcNow
                };

                _dbContext.Tenants.Add(tenant);
                using (_tenantContext.BeginScope(new TenantContext { TenantId = tenant.Id, TenantName = tenant.Name }))
                {
                    _dbContext.AccessApiKeys.Add(accessKey);
                    _dbContext.TenantMemberships.Add(new TenantMembershipEntity
                    {
                        SubjectId = accessKey.Id.ToString(),
                        SubjectType = "ApiKey",
                        Role = accessKey.Role,
                        TenantId = accessKey.TenantId,
                        GrantedAt = DateTime.UtcNow
                    });
                    await _dbContext.SaveChangesAsync(cancellationToken);
                }
                _logger.LogInformation("Bootstrap tenant and configured API key were provisioned.");
            }
            else
            {
                _logger.LogInformation("Database already contains tenants. Tenant bootstrap was skipped.");
            }

            var configuredPlatformAdmins = _configuration
                .GetSection("AgenticSystem:PlatformAdministrators")
                .Get<string[]>() ?? [];
            if (configuredPlatformAdmins.Length > 0 &&
                !await _dbContext.PlatformAdministrators.AnyAsync(cancellationToken))
            {
                var configuredUserIds = configuredPlatformAdmins
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .Select(id => id.Trim())
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
                foreach (var userId in configuredUserIds)
                {
                    _dbContext.PlatformAdministrators.Add(new PlatformAdministratorEntity
                    {
                        UserId = userId,
                        GrantedAt = DateTime.UtcNow,
                        GrantedBy = "configuration-bootstrap"
                    });
                }

                if (configuredUserIds.Length > 0)
                {
                    await _dbContext.SaveChangesAsync(cancellationToken);
                    _logger.LogInformation("Bootstrapped {Count} explicitly configured platform administrators.", configuredUserIds.Length);
                }
            }

            _logger.LogInformation("System bootstrap completed.");
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning(ex, "Bootstrap records were created by another application instance.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during system bootstrap.");
            throw;
        }
    }
}
