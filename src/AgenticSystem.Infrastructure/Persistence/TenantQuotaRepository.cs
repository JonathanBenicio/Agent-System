using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of <see cref="ITenantQuotaRepository"/>.
/// Uses optimistic concurrency via <c>RowVersion</c> and retries on conflict
/// to safely handle concurrent usage increments without serializing all requests.
/// </summary>
public sealed class TenantQuotaRepository : ITenantQuotaRepository
{
    private readonly IDbContextFactory<AgenticDbContext> _dbContextFactory;
    private readonly ILogger<TenantQuotaRepository> _logger;
    private const int MaxRetries = 3;

    public TenantQuotaRepository(
        IDbContextFactory<AgenticDbContext> dbContextFactory,
        ILogger<TenantQuotaRepository> logger)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<TenantQuotaSnapshot> GetOrCreateAsync(string tenantId, CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);

        var entity = await db.TenantQuotas
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(q => q.TenantId == tenantId, ct);

        if (entity is null)
        {
            entity = new TenantQuotaEntity { TenantId = tenantId };
            db.TenantQuotas.Add(entity);
            try
            {
                await db.SaveChangesAsync(ct);
                _logger.LogInformation("Created default quota record for tenant {TenantId}", tenantId);
            }
            catch (DbUpdateException)
            {
                // Race: another instance already created it — reload.
                db.ChangeTracker.Clear();
                entity = await db.TenantQuotas
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(q => q.TenantId == tenantId, ct)
                    ?? new TenantQuotaEntity { TenantId = tenantId };
            }
        }

        // Reset stale daily counters in-memory before returning snapshot.
        if (entity.LastResetAt.Date < DateTime.UtcNow.Date)
        {
            entity.CurrentDailyTokens = 0;
            entity.CurrentDailyCostUsd = 0;
            entity.CurrentDailyRequests = 0;
        }

        return ToSnapshot(entity);
    }

    /// <inheritdoc/>
    public async Task IncrementUsageAsync(string tenantId, int tokens, double costUsd, CancellationToken ct = default)
    {
        for (var attempt = 0; attempt < MaxRetries; attempt++)
        {
            try
            {
                await using var db = await _dbContextFactory.CreateDbContextAsync(ct);

                var entity = await db.TenantQuotas
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(q => q.TenantId == tenantId, ct);

                if (entity is null)
                {
                    entity = new TenantQuotaEntity { TenantId = tenantId };
                    db.TenantQuotas.Add(entity);
                }

                // Reset counters if the day has rolled over.
                if (entity.LastResetAt.Date < DateTime.UtcNow.Date)
                {
                    entity.CurrentDailyTokens = 0;
                    entity.CurrentDailyCostUsd = 0;
                    entity.CurrentDailyRequests = 0;
                    entity.LastResetAt = DateTime.UtcNow.Date;
                }

                entity.CurrentDailyTokens += tokens;
                entity.CurrentDailyCostUsd += costUsd;
                entity.CurrentDailyRequests++;
                entity.UpdatedAt = DateTime.UtcNow;

                await db.SaveChangesAsync(ct);
                return;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogDebug(ex, "Optimistic concurrency conflict incrementing quota for {TenantId} (attempt {Attempt})", tenantId, attempt + 1);
                if (attempt == MaxRetries - 1) throw;
                await Task.Delay(TimeSpan.FromMilliseconds(50 * (attempt + 1)), ct);
            }
        }
    }

    /// <inheritdoc/>
    public async Task UpsertConfigAsync(string tenantId, QuotaConfig config, CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);

        var entity = await db.TenantQuotas
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(q => q.TenantId == tenantId, ct);

        if (entity is null)
        {
            entity = new TenantQuotaEntity { TenantId = tenantId };
            db.TenantQuotas.Add(entity);
        }

        entity.RequestsPerMinute = config.RequestsPerMinute;
        entity.MaxTokensPerDay = config.MaxTokensPerDay;
        entity.MaxDailyBudgetUsd = config.MaxDailyBudgetUsd;
        entity.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        _logger.LogInformation("Updated quota config for tenant {TenantId}: {RPM} req/min, {Tokens} tokens/day, ${Budget}/day",
            tenantId, config.RequestsPerMinute, config.MaxTokensPerDay, config.MaxDailyBudgetUsd);
    }

    /// <inheritdoc/>
    public async Task ResetDailyCountersAsync(CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var today = DateTime.UtcNow.Date;

        var stale = await db.TenantQuotas
            .IgnoreQueryFilters()
            .Where(q => q.LastResetAt < today)
            .ToListAsync(ct);

        if (stale.Count == 0) return;

        foreach (var entity in stale)
        {
            entity.CurrentDailyTokens = 0;
            entity.CurrentDailyCostUsd = 0;
            entity.CurrentDailyRequests = 0;
            entity.LastResetAt = today;
            entity.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);
        _logger.LogInformation("Daily quota counters reset for {Count} tenant(s)", stale.Count);
    }

    private static TenantQuotaSnapshot ToSnapshot(TenantQuotaEntity e) => new(
        e.TenantId,
        e.RequestsPerMinute,
        e.MaxTokensPerDay,
        e.MaxDailyBudgetUsd,
        e.CurrentDailyTokens,
        e.CurrentDailyCostUsd,
        e.CurrentDailyRequests);
}
