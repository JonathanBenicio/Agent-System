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
    private readonly Dictionary<string, DateOnly> _usageDates = [];
    private readonly TimeProvider _clock;

    public InMemoryTenantQuotaRepository(TimeProvider? clock = null) => _clock = clock ?? TimeProvider.System;

    private TenantQuotaSnapshot GetCurrentSnapshot(string tenantId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        var today = DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);
        var snapshot = _store.TryGetValue(tenantId, out var existing)
            ? existing : new TenantQuotaSnapshot(tenantId, 30, 1_000_000, 50.0, 0, 0, 0);
        if (!_usageDates.TryGetValue(tenantId, out var date) || date < today)
        {
            snapshot = snapshot with { CurrentDailyTokens = 0, CurrentDailyCostUsd = 0, CurrentDailyRequests = 0 };
            _usageDates[tenantId] = today;
        }
        _store[tenantId] = snapshot;
        return snapshot;
    }

    public Task<TenantQuotaSnapshot> GetOrCreateAsync(string tenantId, CancellationToken ct = default)
    {
        lock (_lock)
        {
            return Task.FromResult(GetCurrentSnapshot(tenantId));
        }
    }

    public Task IncrementUsageAsync(string tenantId, int tokens, double costUsd, CancellationToken ct = default)
    {
        lock (_lock)
        {
            var existing = GetCurrentSnapshot(tenantId);

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
            var existing = GetCurrentSnapshot(tenantId);

            _store[tenantId] = existing with
            {
                RequestsPerMinute = config.RequestsPerMinute,
                MaxTokensPerDay = config.MaxTokensPerDay,
                MaxDailyBudgetUsd = config.MaxDailyBudgetUsd
            };
        }

        return Task.CompletedTask;
    }

    public Task ResetDailyCountersAsync(string tenantId, CancellationToken ct = default)
    {
        lock (_lock)
        {
            GetCurrentSnapshot(tenantId);
        }

        return Task.CompletedTask;
    }
}
