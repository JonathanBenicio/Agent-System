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
    private readonly ISystemOperationContextAccessor _systemOperations;

    public TenantResolver(
        ITenantStore tenantStore,
        ILogger<TenantResolver> logger,
        ISystemOperationContextAccessor systemOperations)
    {
        _tenantStore = tenantStore;
        _logger = logger;
        _systemOperations = systemOperations;
    }

    public async Task<TenantContext?> ResolveAsync(string tenantId)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            return null;

        if (TenantIdPolicy.IsReservedSystemId(tenantId))
        {
            _logger.LogWarning("Reserved system operation ID cannot resolve as a tenant: {TenantId}", tenantId);
            return null;
        }

        using var systemScope = _systemOperations.BeginScope(SystemOperationKind.TenantResolution);
        _systemOperations.Require(SystemOperationKind.TenantResolution);
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
