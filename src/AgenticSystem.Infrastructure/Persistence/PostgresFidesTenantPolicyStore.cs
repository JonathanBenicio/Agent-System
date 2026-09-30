using System.Text.Json;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgenticSystem.Infrastructure.Persistence;

public sealed class PostgresFidesTenantPolicyStore : IFidesTenantPolicyStore
{
    private readonly IDbContextFactory<AgenticDbContext> _dbContextFactory;
    private readonly ITenantContextAccessor _tenantContextAccessor;

    public PostgresFidesTenantPolicyStore(
        IDbContextFactory<AgenticDbContext> dbContextFactory,
        ITenantContextAccessor tenantContextAccessor)
    {
        _dbContextFactory = dbContextFactory;
        _tenantContextAccessor = tenantContextAccessor;
    }

    public async Task<FidesTenantPolicy> GetAsync(CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var entity = await db.FidesTenantPolicies.AsNoTracking().SingleOrDefaultAsync(ct);
        if (entity is null)
        {
            return new FidesTenantPolicy
            {
                TenantId = _tenantContextAccessor.CurrentTenantId,
                EnabledDetectors = FidesDetectorCatalog.Normalize(null)
            };
        }

        var stored = JsonSerializer.Deserialize<Dictionary<string, bool>>(entity.EnabledDetectorsJson);
        var normalized = FidesDetectorCatalog.Normalize(stored);
        return new FidesTenantPolicy
        {
            TenantId = entity.TenantId,
            EnabledDetectors = normalized,
            Version = entity.Version,
            UpdatedBy = entity.UpdatedBy,
            UpdatedAt = entity.UpdatedAt
        };
    }

    public async Task<FidesTenantPolicy> SaveAsync(
        IReadOnlyDictionary<string, bool> enabledDetectors,
        string updatedBy,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(updatedBy))
            throw new ArgumentException("An actor ID is required to update FIDES policy.", nameof(updatedBy));

        var tenantId = _tenantContextAccessor.CurrentTenantId;
        var normalized = FidesDetectorCatalog.Normalize(enabledDetectors);
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var entity = await db.FidesTenantPolicies.SingleOrDefaultAsync(ct);
        if (entity is null)
        {
            entity = new FidesTenantPolicyEntity { TenantId = tenantId, Version = 1 };
            db.FidesTenantPolicies.Add(entity);
        }
        else
        {
            entity.Version++;
        }

        entity.EnabledDetectorsJson = JsonSerializer.Serialize(normalized);
        entity.UpdatedBy = updatedBy;
        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return new FidesTenantPolicy
        {
            TenantId = tenantId,
            EnabledDetectors = normalized,
            Version = entity.Version,
            UpdatedBy = entity.UpdatedBy,
            UpdatedAt = entity.UpdatedAt
        };
    }
}
