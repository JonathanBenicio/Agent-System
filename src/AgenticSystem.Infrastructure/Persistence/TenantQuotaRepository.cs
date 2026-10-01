using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of <see cref="ITenantQuotaRepository"/>.
/// Uses single-statement PostgreSQL upserts so concurrent instances cannot lose usage updates.
/// </summary>
public sealed class TenantQuotaRepository : ITenantQuotaRepository
{
    private readonly IDbContextFactory<AgenticDbContext> _dbContextFactory;
    private readonly ILogger<TenantQuotaRepository> _logger;
    private readonly ITenantContextAccessor _tenantAccessor;

    public TenantQuotaRepository(
        IDbContextFactory<AgenticDbContext> dbContextFactory,
        ILogger<TenantQuotaRepository> logger,
        ITenantContextAccessor tenantAccessor)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
        _tenantAccessor = tenantAccessor;
    }

    /// <inheritdoc/>
    public async Task<TenantQuotaSnapshot> GetOrCreateAsync(string tenantId, CancellationToken ct = default)
    {
        TenantContextPolicy.RequireCurrentTenant(_tenantAccessor, tenantId);
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);

        var entity = await db.TenantQuotas
            .AsNoTracking()
            .FirstOrDefaultAsync(q => q.TenantId == tenantId, ct);

        if (entity is null)
        {
            var today = DateTime.UtcNow.Date;
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO tenant_quotas ("TenantId", "RequestsPerMinute", "MaxTokensPerDay", "MaxDailyBudgetUsd",
                    "CurrentDailyTokens", "CurrentDailyCostUsd", "CurrentDailyRequests", "LastResetAt", "UpdatedAt")
                VALUES ({tenantId}, 30, 1000000, 50.0, 0, 0, 0, {today}, {DateTime.UtcNow})
                ON CONFLICT ("TenantId") DO NOTHING
                """, ct);
            entity = await db.TenantQuotas.AsNoTracking()
                .FirstOrDefaultAsync(q => q.TenantId == tenantId, ct)
                ?? throw new InvalidOperationException($"Quota row for tenant '{tenantId}' could not be created or loaded.");
            _logger.LogInformation("Created default quota record for tenant {TenantId}", tenantId);
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
        TenantContextPolicy.RequireCurrentTenant(_tenantAccessor, tenantId);
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var today = DateTime.UtcNow.Date;
        var now = DateTime.UtcNow;
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO tenant_quotas ("TenantId", "RequestsPerMinute", "MaxTokensPerDay", "MaxDailyBudgetUsd",
                "CurrentDailyTokens", "CurrentDailyCostUsd", "CurrentDailyRequests", "LastResetAt", "UpdatedAt")
            VALUES ({tenantId}, 30, 1000000, 50.0, {tokens}, {costUsd}, 1, {today}, {now})
            ON CONFLICT ("TenantId") DO UPDATE SET
                "CurrentDailyTokens" = CASE WHEN tenant_quotas."LastResetAt" < {today} THEN {tokens} ELSE tenant_quotas."CurrentDailyTokens" + {tokens} END,
                "CurrentDailyCostUsd" = CASE WHEN tenant_quotas."LastResetAt" < {today} THEN {costUsd} ELSE tenant_quotas."CurrentDailyCostUsd" + {costUsd} END,
                "CurrentDailyRequests" = CASE WHEN tenant_quotas."LastResetAt" < {today} THEN 1 ELSE tenant_quotas."CurrentDailyRequests" + 1 END,
                "LastResetAt" = CASE WHEN tenant_quotas."LastResetAt" < {today} THEN {today} ELSE tenant_quotas."LastResetAt" END,
                "UpdatedAt" = {now}
            """, ct);
    }

    /// <inheritdoc/>
    public async Task UpsertConfigAsync(string tenantId, QuotaConfig config, CancellationToken ct = default)
    {
        TenantContextPolicy.RequireCurrentTenant(_tenantAccessor, tenantId);
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);

        var entity = await db.TenantQuotas
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
    public async Task ResetDailyCountersAsync(string tenantId, CancellationToken ct = default)
    {
        TenantContextPolicy.RequireCurrentTenant(_tenantAccessor, tenantId);
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var today = DateTime.UtcNow.Date;

        var updated = await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE tenant_quotas
            SET "CurrentDailyTokens" = 0, "CurrentDailyCostUsd" = 0, "CurrentDailyRequests" = 0,
                "LastResetAt" = {today}, "UpdatedAt" = {DateTime.UtcNow}
            WHERE "TenantId" = {tenantId} AND "LastResetAt" < {today}
            """, ct);
        _logger.LogInformation("Daily quota counters reset for tenant {TenantId}; changed {Count} row(s)", tenantId, updated);
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
