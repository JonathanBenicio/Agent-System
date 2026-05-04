using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Interfaces;

/// <summary>
/// Abstração para persistência de tenants.
/// </summary>
public interface ITenantStore
{
    Task<Tenant?> GetByIdAsync(string tenantId, CancellationToken ct = default);
    Task<Tenant?> GetBySlugAsync(string slug, CancellationToken ct = default);
    Task<IReadOnlyList<Tenant>> GetAllAsync(CancellationToken ct = default);
    Task SaveAsync(Tenant tenant, CancellationToken ct = default);
    Task DeleteAsync(string tenantId, CancellationToken ct = default);
    Task<bool> ExistsAsync(string tenantId, CancellationToken ct = default);
}
