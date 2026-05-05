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

    public EfTenantStore(IServiceScopeFactory scopeFactory, ILogger<EfTenantStore> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<Tenant?> GetByIdAsync(string tenantId, CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AgenticDbContext>();
        await EnsureDefaultTenantAsync(db, ct);
        return await db.Tenants.AsNoTracking().FirstOrDefaultAsync(tenant => tenant.Id == tenantId, ct);
    }

    public async Task<Tenant?> GetBySlugAsync(string slug, CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AgenticDbContext>();
        await EnsureDefaultTenantAsync(db, ct);
        return await db.Tenants.AsNoTracking().FirstOrDefaultAsync(tenant => tenant.Slug == slug, ct);
    }

    public async Task<IReadOnlyList<Tenant>> GetAllAsync(CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AgenticDbContext>();
        await EnsureDefaultTenantAsync(db, ct);
        return await db.Tenants.AsNoTracking().OrderBy(tenant => tenant.Name).ToListAsync(ct);
    }

    public async Task SaveAsync(Tenant tenant, CancellationToken ct = default)
    {
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
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AgenticDbContext>();
        await EnsureDefaultTenantAsync(db, ct);
        return await db.Tenants.AnyAsync(tenant => tenant.Id == tenantId, ct);
    }

    private async Task EnsureDefaultTenantAsync(AgenticDbContext db, CancellationToken ct)
    {
        if (await db.Tenants.AnyAsync(tenant => tenant.Id == Tenant.DefaultTenantId, ct))
        {
            return;
        }

        db.Tenants.Add(new Tenant
        {
            Id = Tenant.DefaultTenantId,
            Name = "Default",
            Slug = "default",
            Plan = TenantPlan.Pro,
            Limits = TenantLimits.ProTier(),
            IsActive = true,
            UpdatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync(ct);
        _logger.LogInformation("Default tenant created in PostgreSQL tenant store");
    }
}