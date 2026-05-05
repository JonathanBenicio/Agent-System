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
    private readonly TimeSpan _ttl = TimeSpan.FromHours(24);
    private readonly int _maxEntries = 10_000;
    private DateTime _lastCleanup = DateTime.UtcNow;

    public Task SaveAsync(SessionData session, CancellationToken ct = default)
    {
        EvictStaleEntriesIfNeeded();
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

    private void EvictStaleEntriesIfNeeded()
    {
        if (DateTime.UtcNow - _lastCleanup < TimeSpan.FromMinutes(5) && _store.Count < _maxEntries)
            return;

        _lastCleanup = DateTime.UtcNow;
        var cutoff = DateTime.UtcNow - _ttl;

        var staleKeys = _store
            .Where(kv => kv.Value.StartedAt < cutoff)
            .Select(kv => kv.Key)
            .ToList();

        foreach (var key in staleKeys)
            _store.TryRemove(key, out _);

        // If still over limit, remove oldest
        if (_store.Count > _maxEntries)
        {
            var toRemove = _store
                .OrderBy(kv => kv.Value.StartedAt)
                .Take(_store.Count - _maxEntries)
                .Select(kv => kv.Key)
                .ToList();

            foreach (var key in toRemove)
                _store.TryRemove(key, out _);
        }
    }
}
