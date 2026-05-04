using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Interfaces;

/// <summary>
/// Resolve o TenantContext a partir de claims de identidade.
/// Implementado na camada de API (TenantMiddleware).
/// </summary>
public interface ITenantResolver
{
    /// <summary>
    /// Resolve TenantContext a partir de um tenantId extraído do token/header.
    /// </summary>
    Task<TenantContext?> ResolveAsync(string tenantId);
}
