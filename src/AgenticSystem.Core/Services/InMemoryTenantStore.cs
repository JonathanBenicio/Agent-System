using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using System.Collections.Concurrent;

namespace AgenticSystem.Core.Services;

/// <summary>
/// Implementação in-memory de ITenantStore para testes e modo não persistente.
/// </summary>
public class InMemoryTenantStore : ITenantStore
{
    private readonly ConcurrentDictionary<string, Tenant> _tenants = new();

    public InMemoryTenantStore()
    {
    }

    public Task<Tenant?> GetByIdAsync(string tenantId, CancellationToken ct = default)
    {
        if (TenantIdPolicy.IsReservedSystemId(tenantId))
            return Task.FromResult<Tenant?>(null);
        _tenants.TryGetValue(tenantId, out var tenant);
        return Task.FromResult(tenant);
    }

    public Task<Tenant?> GetBySlugAsync(string slug, CancellationToken ct = default)
    {
        var tenant = _tenants.Values.FirstOrDefault(t =>
            !TenantIdPolicy.IsReservedSystemId(t.Id) && t.Slug.Equals(slug, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(tenant);
    }

    public Task<IReadOnlyList<Tenant>> GetAllAsync(CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<Tenant>>(_tenants.Values
            .Where(tenant => !TenantIdPolicy.IsReservedSystemId(tenant.Id))
            .ToList());
    }

    public Task SaveAsync(Tenant tenant, CancellationToken ct = default)
    {
        if (TenantIdPolicy.IsReservedSystemId(tenant.Id))
            throw new ArgumentException("Reserved system identifiers and 'default' cannot be persisted as tenants.", nameof(tenant));
        tenant.UpdatedAt = DateTime.UtcNow;
        _tenants[tenant.Id] = tenant;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string tenantId, CancellationToken ct = default)
    {
        if (TenantIdPolicy.IsReservedSystemId(tenantId))
            throw new ArgumentException("Reserved system identifiers and 'default' cannot be deleted as tenants.", nameof(tenantId));
        _tenants.TryRemove(tenantId, out _);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string tenantId, CancellationToken ct = default)
    {
        if (TenantIdPolicy.IsReservedSystemId(tenantId))
            return Task.FromResult(false);
        return Task.FromResult(_tenants.ContainsKey(tenantId));
    }
}
