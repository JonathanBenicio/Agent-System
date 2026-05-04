using System.Collections.Concurrent;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Services;

/// <summary>
/// Store in-memory de configurações. Em produção substituir por PostgreSQL.
/// </summary>
public class InMemoryConfigStore : IConfigStore
{
    private readonly ConcurrentDictionary<string, ConfigEntry> _entries = new();
    private readonly ConcurrentBag<ConfigChangeLog> _changeLogs = new();

    public Task<ConfigEntry?> GetByKeyAsync(string key)
    {
        _entries.TryGetValue(key, out var entry);
        return Task.FromResult(entry);
    }

    public Task<IEnumerable<ConfigEntry>> GetAllAsync(ConfigCategory? category = null)
    {
        var results = _entries.Values.AsEnumerable();
        if (category.HasValue)
            results = results.Where(e => e.Category == category.Value);
        return Task.FromResult(results);
    }

    public Task SaveAsync(ConfigEntry entry)
    {
        _entries[entry.Key] = entry;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string key)
    {
        _entries.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    public Task<IEnumerable<ConfigChangeLog>> GetChangeLogsAsync(string? key = null, int limit = 50)
    {
        var logs = _changeLogs.AsEnumerable();
        if (!string.IsNullOrEmpty(key))
            logs = logs.Where(l => l.ConfigKey == key);
        return Task.FromResult(logs.OrderByDescending(l => l.ChangedAt).Take(limit));
    }

    public Task SaveChangeLogAsync(ConfigChangeLog log)
    {
        _changeLogs.Add(log);
        return Task.CompletedTask;
    }
}
