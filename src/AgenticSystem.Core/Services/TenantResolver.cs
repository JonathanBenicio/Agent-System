using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Core.Services;

/// <summary>
/// Resolve TenantContext a partir de um tenantId.
/// Busca no ITenantStore e monta o contexto com limites do plano.
/// </summary>
public class TenantResolver : ITenantResolver
{
    private readonly ITenantStore _tenantStore;
    private readonly ILogger<TenantResolver> _logger;
    private readonly bool _isDevelopment;

    public TenantResolver(ITenantStore tenantStore, ILogger<TenantResolver> logger)
        : this(tenantStore, logger, isDevelopment: true)
    {
    }

    public TenantResolver(ITenantStore tenantStore, ILogger<TenantResolver> logger, IHostEnvironment environment)
        : this(tenantStore, logger, environment.IsDevelopment())
    {
    }

    public TenantResolver(ITenantStore tenantStore, ILogger<TenantResolver> logger, bool isDevelopment)
    {
        _tenantStore = tenantStore;
        _logger = logger;
        _isDevelopment = isDevelopment;
    }

    public async Task<TenantContext?> ResolveAsync(string tenantId)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            return null;

        var tenant = await _tenantStore.GetByIdAsync(tenantId);
        if (tenant is null || !tenant.IsActive)
        {
            _logger.LogWarning("Tenant not found or inactive: {TenantId}", tenantId);

            if (_isDevelopment)
            {
                _logger.LogInformation("Accepting unknown tenant '{TenantId}' for dev/test scenario.", tenantId);
                return new TenantContext
                {
                    TenantId = tenantId,
                    TenantName = $"Dev Tenant ({tenantId})",
                    Plan = TenantPlan.Pro,
                    Limits = TenantLimits.ProTier(),
                    IsAuthenticated = true
                };
            }

            return null;
        }

        return new TenantContext
        {
            TenantId = tenant.Id,
            TenantName = tenant.Name,
            Plan = tenant.Plan,
            Limits = tenant.Limits,
            IsAuthenticated = true
        };
    }
}
