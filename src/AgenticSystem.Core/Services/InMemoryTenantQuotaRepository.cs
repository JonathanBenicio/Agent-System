using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Services;

/// <summary>
/// In-memory implementation of <see cref="ITenantQuotaRepository"/> used when
/// the application is running in InMemory or SQLite storage mode (non-production).
/// Data is not persisted across restarts.
/// </summary>
public sealed class InMemoryTenantQuotaRepository : ITenantQuotaRepository
{
    private readonly Dictionary<string, TenantQuotaSnapshot> _store = [];
    private readonly Lock _lock = new();

    public Task<TenantQuotaSnapshot> GetOrCreateAsync(string tenantId, CancellationToken ct = default)
    {
        lock (_lock)
        {
            if (!_store.TryGetValue(tenantId, out var snapshot))
            {
                snapshot = new TenantQuotaSnapshot(tenantId, 30, 1_000_000, 50.0, 0, 0, 0);
                _store[tenantId] = snapshot;
            }

            // Reset daily counters if stale
            return Task.FromResult(snapshot);
        }
    }

    public Task IncrementUsageAsync(string tenantId, int tokens, double costUsd, CancellationToken ct = default)
    {
        lock (_lock)
        {
            var existing = _store.TryGetValue(tenantId, out var s)
                ? s
                : new TenantQuotaSnapshot(tenantId, 30, 1_000_000, 50.0, 0, 0, 0);

            _store[tenantId] = existing with
            {
                CurrentDailyTokens = existing.CurrentDailyTokens + tokens,
                CurrentDailyCostUsd = existing.CurrentDailyCostUsd + costUsd,
                CurrentDailyRequests = existing.CurrentDailyRequests + 1
            };
        }

        return Task.CompletedTask;
    }

    public Task UpsertConfigAsync(string tenantId, QuotaConfig config, CancellationToken ct = default)
    {
        lock (_lock)
        {
            var existing = _store.TryGetValue(tenantId, out var s)
                ? s
                : new TenantQuotaSnapshot(tenantId, config.RequestsPerMinute, config.MaxTokensPerDay, config.MaxDailyBudgetUsd, 0, 0, 0);

            _store[tenantId] = existing with
            {
                RequestsPerMinute = config.RequestsPerMinute,
                MaxTokensPerDay = config.MaxTokensPerDay,
                MaxDailyBudgetUsd = config.MaxDailyBudgetUsd
            };
        }

        return Task.CompletedTask;
    }

    public Task ResetDailyCountersAsync(CancellationToken ct = default)
    {
        lock (_lock)
        {
            var keys = _store.Keys.ToList();
            foreach (var key in keys)
            {
                var s = _store[key];
                _store[key] = s with { CurrentDailyTokens = 0, CurrentDailyCostUsd = 0, CurrentDailyRequests = 0 };
            }
        }

        return Task.CompletedTask;
    }
}
