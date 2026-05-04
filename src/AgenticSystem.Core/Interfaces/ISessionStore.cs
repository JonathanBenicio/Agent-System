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
    Task<IReadOnlyList<SessionData>> GetByUserAsync(string userId, int maxResults = 10, CancellationToken ct = default);
    Task<IReadOnlyList<SessionData>> GetByTenantAsync(string tenantId, string? userId = null, int maxResults = 10, CancellationToken ct = default);
    Task DeleteAsync(string sessionId, CancellationToken ct = default);
    Task<bool> ExistsAsync(string sessionId, CancellationToken ct = default);
}
