using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using System.Collections.Concurrent;

namespace AgenticSystem.Infrastructure.Persistence;

public class InMemoryDynamicAgentRepository : IDynamicAgentRepository
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, AgentSpecification>> _tenants = new(StringComparer.Ordinal);
    private readonly ITenantContextAccessor _tenantContext;

    public InMemoryDynamicAgentRepository(ITenantContextAccessor tenantContext) => _tenantContext = tenantContext;

    private ConcurrentDictionary<string, AgentSpecification> CurrentStore
    {
        get
        {
            var tenant = TenantContextPolicy.RequireCurrentTenant(_tenantContext, _tenantContext.CurrentTenantId);
            return _tenants.GetOrAdd(tenant, _ => new(StringComparer.OrdinalIgnoreCase));
        }
    }

    public Task<IEnumerable<AgentSpecification>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IEnumerable<AgentSpecification>>(CurrentStore.Values.ToArray());
    }

    public Task<AgentSpecification?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        CurrentStore.TryGetValue(name, out var spec);
        return Task.FromResult(spec);
    }

    public Task SaveAsync(AgentSpecification specification, CancellationToken cancellationToken = default)
    {
        CurrentStore[specification.Name] = specification;
        return Task.CompletedTask;
    }

    public Task<bool> DeactivateAsync(string name, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(CurrentStore.TryRemove(name, out _));
    }
}
