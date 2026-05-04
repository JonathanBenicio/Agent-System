using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using System.Collections.Concurrent;

namespace AgenticSystem.Core.Services;

/// <summary>
/// Implementação in-memory de ITenantStore.
/// Inicializa com um tenant "default" para backward compatibility.
/// </summary>
public class InMemoryTenantStore : ITenantStore
{
    private readonly ConcurrentDictionary<string, Tenant> _tenants = new();

    public InMemoryTenantStore()
    {
        // Seed default tenant para backward compatibility
        var defaultTenant = new Tenant
        {
            Id = Tenant.DefaultTenantId,
            Name = "Default",
            Slug = "default",
            Plan = TenantPlan.Pro,
            Limits = TenantLimits.ProTier(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        _tenants[defaultTenant.Id] = defaultTenant;
    }

    public Task<Tenant?> GetByIdAsync(string tenantId, CancellationToken ct = default)
    {
        _tenants.TryGetValue(tenantId, out var tenant);
        return Task.FromResult(tenant);
    }

    public Task<Tenant?> GetBySlugAsync(string slug, CancellationToken ct = default)
    {
        var tenant = _tenants.Values.FirstOrDefault(
            t => t.Slug.Equals(slug, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(tenant);
    }

    public Task<IReadOnlyList<Tenant>> GetAllAsync(CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<Tenant>>(_tenants.Values.ToList());
    }

    public Task SaveAsync(Tenant tenant, CancellationToken ct = default)
    {
        tenant.UpdatedAt = DateTime.UtcNow;
        _tenants[tenant.Id] = tenant;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string tenantId, CancellationToken ct = default)
    {
        _tenants.TryRemove(tenantId, out _);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string tenantId, CancellationToken ct = default)
    {
        return Task.FromResult(_tenants.ContainsKey(tenantId));
    }
}
