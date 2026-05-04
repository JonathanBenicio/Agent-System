using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using System.Collections.Concurrent;

namespace AgenticSystem.Core.Services;

/// <summary>
/// Implementação in-memory de ISessionStore.
/// Apenas para desenvolvimento e testes — em produção, use PostgresSessionStore.
/// </summary>
public class InMemorySessionStore : ISessionStore
{
    private readonly ConcurrentDictionary<string, SessionData> _store = new();

    public Task SaveAsync(SessionData session, CancellationToken ct = default)
    {
        _store[session.Id] = session;
        return Task.CompletedTask;
    }

    public Task<SessionData?> GetAsync(string sessionId, CancellationToken ct = default)
    {
        _store.TryGetValue(sessionId, out var session);
        return Task.FromResult(session);
    }

    public Task<IReadOnlyList<SessionData>> GetByUserAsync(string userId, int maxResults = 10, CancellationToken ct = default)
    {
        var sessions = _store.Values
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.StartedAt)
            .Take(maxResults)
            .ToList();
        return Task.FromResult<IReadOnlyList<SessionData>>(sessions);
    }

    /// <summary>
    /// Busca sessões por tenant e user (multi-tenant query).
    /// </summary>
    public Task<IReadOnlyList<SessionData>> GetByTenantAsync(string tenantId, string? userId = null, int maxResults = 10, CancellationToken ct = default)
    {
        var query = _store.Values.Where(s => s.TenantId == tenantId);
        if (userId is not null)
            query = query.Where(s => s.UserId == userId);

        var sessions = query
            .OrderByDescending(s => s.StartedAt)
            .Take(maxResults)
            .ToList();
        return Task.FromResult<IReadOnlyList<SessionData>>(sessions);
    }

    public Task DeleteAsync(string sessionId, CancellationToken ct = default)
    {
        _store.TryRemove(sessionId, out _);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string sessionId, CancellationToken ct = default)
    {
        return Task.FromResult(_store.ContainsKey(sessionId));
    }
}
