using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Services;

public sealed class InMemoryFidesTenantPolicyStore : IFidesTenantPolicyStore
{
    private readonly Dictionary<string, FidesTenantPolicy> _policies = new(StringComparer.Ordinal);
    private readonly ITenantContextAccessor _tenantContextAccessor;
    private readonly object _sync = new();

    public InMemoryFidesTenantPolicyStore(ITenantContextAccessor tenantContextAccessor)
    {
        _tenantContextAccessor = tenantContextAccessor;
    }

    public Task<FidesTenantPolicy> GetAsync(CancellationToken ct = default)
    {
        var tenantId = _tenantContextAccessor.CurrentTenantId;
        lock (_sync)
        {
            if (_policies.TryGetValue(tenantId, out var policy))
                return Task.FromResult(Clone(policy));
        }

        return Task.FromResult(new FidesTenantPolicy
        {
            TenantId = tenantId,
            EnabledDetectors = FidesDetectorCatalog.Normalize(null)
        });
    }

    public async Task<FidesTenantPolicy> SaveAsync(
        IReadOnlyDictionary<string, bool> enabledDetectors,
        string updatedBy,
        CancellationToken ct = default)
    {
        var tenantId = _tenantContextAccessor.CurrentTenantId;
        var normalized = FidesDetectorCatalog.Normalize(enabledDetectors);
        FidesTenantPolicy policy;
        lock (_sync)
        {
            var version = _policies.TryGetValue(tenantId, out var current) ? current.Version + 1 : 1;
            policy = new FidesTenantPolicy
            {
                TenantId = tenantId,
                EnabledDetectors = normalized,
                Version = version,
                UpdatedBy = updatedBy,
                UpdatedAt = DateTime.UtcNow
            };
            _policies[tenantId] = policy;
        }

        return await Task.FromResult(Clone(policy));
    }

    private static FidesTenantPolicy Clone(FidesTenantPolicy policy) => new()
    {
        TenantId = policy.TenantId,
        EnabledDetectors = new Dictionary<string, bool>(policy.EnabledDetectors, StringComparer.OrdinalIgnoreCase),
        Version = policy.Version,
        UpdatedBy = policy.UpdatedBy,
        UpdatedAt = policy.UpdatedAt
    };
}
