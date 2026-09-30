using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Infrastructure.Persistence;

public class InMemoryDynamicAgentRepository : IDynamicAgentRepository
{
    private readonly Dictionary<string, AgentSpecification> _store = new(StringComparer.OrdinalIgnoreCase);

    public Task<IEnumerable<AgentSpecification>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IEnumerable<AgentSpecification>>(_store.Values);
    }

    public Task<AgentSpecification?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        _store.TryGetValue(name, out var spec);
        return Task.FromResult(spec);
    }

    public Task SaveAsync(AgentSpecification specification, CancellationToken cancellationToken = default)
    {
        _store[specification.Name] = specification;
        return Task.CompletedTask;
    }

    public Task<bool> DeactivateAsync(string name, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_store.Remove(name));
    }
}
