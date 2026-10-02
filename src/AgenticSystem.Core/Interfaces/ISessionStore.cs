using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Interfaces;

/// <summary>
/// Abstração para persistência de sessões.
/// Implementações: InMemorySessionStore (dev/test), PostgresSessionStore (produção).
/// </summary>
public interface ISessionStore
{
    Task SaveAsync(SessionData session, CancellationToken ct = default);
    Task<SessionData?> GetAsync(string sessionId, CancellationToken ct = default);
    Task<IReadOnlyList<SessionData>> GetByUserAsync(string userId, int maxResults = 10, string? search = null, CancellationToken ct = default);
    Task<IReadOnlyList<SessionData>> GetByTenantAsync(string tenantId, string? userId = null, int maxResults = 10, CancellationToken ct = default);
    Task DeleteAsync(string sessionId, CancellationToken ct = default);
    Task<bool> ExistsAsync(string sessionId, CancellationToken ct = default);
    /// <summary>Counts every active session in the tenant, without history pagination.</summary>
    async Task<int> CountActiveAsync(string tenantId, CancellationToken ct = default)
        => (await GetByTenantAsync(tenantId, maxResults: int.MaxValue, ct: ct)).Count(session => session.EndedAt is null);
    /// <summary>Atomically reserves a new active session under the tenant ceiling.</summary>
    Task<bool> TryCreateAsync(SessionData session, int maxActive, CancellationToken ct = default)
        => throw new NotSupportedException("This session store does not implement atomic creation.");
}
