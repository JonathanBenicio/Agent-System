using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Infrastructure.Persistence;

public sealed class EfTenantStore : ITenantStore
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EfTenantStore> _logger;
    private readonly ISystemOperationContextAccessor _systemOperations;

    public EfTenantStore(
        IServiceScopeFactory scopeFactory,
        ILogger<EfTenantStore> logger,
        ISystemOperationContextAccessor systemOperations)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _systemOperations = systemOperations;
    }

    public async Task<Tenant?> GetByIdAsync(string tenantId, CancellationToken ct = default)
    {
        if (TenantIdPolicy.IsReservedSystemId(tenantId)) return null;
        using var systemScope = _systemOperations.BeginScope(SystemOperationKind.TenantResolution);
        _systemOperations.Require(SystemOperationKind.TenantResolution);
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AgenticDbContext>();
        return await db.Tenants.AsNoTracking().FirstOrDefaultAsync(tenant => tenant.Id == tenantId, ct);
    }

    public async Task<Tenant?> GetBySlugAsync(string slug, CancellationToken ct = default)
    {
        using var systemScope = _systemOperations.BeginScope(SystemOperationKind.TenantResolution);
        _systemOperations.Require(SystemOperationKind.TenantResolution);
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AgenticDbContext>();
        var tenant = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(item => item.Slug == slug, ct);
        return tenant is not null && !TenantIdPolicy.IsReservedSystemId(tenant.Id) ? tenant : null;
    }

    public async Task<IReadOnlyList<Tenant>> GetAllAsync(CancellationToken ct = default)
    {
        using var systemScope = _systemOperations.BeginScope(SystemOperationKind.EnumerateTenantsForBackground);
        _systemOperations.Require(SystemOperationKind.EnumerateTenantsForBackground);
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AgenticDbContext>();
        var tenants = await db.Tenants.AsNoTracking().OrderBy(tenant => tenant.Name).ToListAsync(ct);
        return tenants.Where(tenant => !TenantIdPolicy.IsReservedSystemId(tenant.Id)).ToArray();
    }

    public async Task SaveAsync(Tenant tenant, CancellationToken ct = default)
    {
        if (TenantIdPolicy.IsReservedSystemId(tenant.Id))
            throw new ArgumentException("Reserved system identifiers and 'default' cannot be persisted as tenants.", nameof(tenant));
        using var systemScope = _systemOperations.BeginScope(SystemOperationKind.TenantRegistryWrite);
        _systemOperations.Require(SystemOperationKind.TenantRegistryWrite);
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AgenticDbContext>();

        var existing = await db.Tenants.FirstOrDefaultAsync(item => item.Id == tenant.Id, ct);
        if (existing is null)
        {
            tenant.UpdatedAt = DateTime.UtcNow;
            db.Tenants.Add(tenant);
        }
        else
        {
            existing.Name = tenant.Name;
            existing.Slug = tenant.Slug;
            existing.Plan = tenant.Plan;
            existing.Limits = tenant.Limits;
            existing.IsActive = tenant.IsActive;
            existing.ProviderApiKeys = tenant.ProviderApiKeys;
            existing.Settings = tenant.Settings;
            existing.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(string tenantId, CancellationToken ct = default)
    {
        if (TenantIdPolicy.IsReservedSystemId(tenantId))
            throw new ArgumentException("Reserved system identifiers and 'default' cannot be deleted as tenants.", nameof(tenantId));
        using var systemScope = _systemOperations.BeginScope(SystemOperationKind.TenantRegistryWrite);
        _systemOperations.Require(SystemOperationKind.TenantRegistryWrite);
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AgenticDbContext>();
        var tenant = await db.Tenants.FirstOrDefaultAsync(item => item.Id == tenantId, ct);
        if (tenant is null)
        {
            return;
        }

        db.Tenants.Remove(tenant);
        await db.SaveChangesAsync(ct);
    }

    public async Task<bool> ExistsAsync(string tenantId, CancellationToken ct = default)
    {
        if (TenantIdPolicy.IsReservedSystemId(tenantId)) return false;
        using var systemScope = _systemOperations.BeginScope(SystemOperationKind.TenantResolution);
        _systemOperations.Require(SystemOperationKind.TenantResolution);
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AgenticDbContext>();
        return await db.Tenants.AnyAsync(tenant => tenant.Id == tenantId, ct);
    }
}
