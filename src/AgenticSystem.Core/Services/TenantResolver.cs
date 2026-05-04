using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
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

    public TenantResolver(ITenantStore tenantStore, ILogger<TenantResolver> logger)
    {
        _tenantStore = tenantStore;
        _logger = logger;
    }

    public async Task<TenantContext?> ResolveAsync(string tenantId)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            return null;

        var tenant = await _tenantStore.GetByIdAsync(tenantId);
        if (tenant is null || !tenant.IsActive)
        {
            _logger.LogWarning("Tenant not found or inactive: {TenantId}", tenantId);
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
