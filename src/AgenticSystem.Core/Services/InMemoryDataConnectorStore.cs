using System.Collections.Concurrent;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Services;

public class InMemoryDataConnectorStore : IDataConnectorStore
{
    private readonly ConcurrentDictionary<string, DataConnectorConfig> _connectors = new();

    public Task<DataConnectorConfig> SaveAsync(DataConnectorConfig config, CancellationToken ct = default)
    {
        _connectors[config.Id] = config;
        return Task.FromResult(config);
    }

    public Task<DataConnectorConfig?> GetAsync(string id, CancellationToken ct = default)
    {
        _connectors.TryGetValue(id, out var config);
        return Task.FromResult(config);
    }

    public Task<IReadOnlyList<DataConnectorConfig>> ListAsync(string? tenantId = null, CancellationToken ct = default)
    {
        var query = _connectors.Values.AsQueryable();
        if (!string.IsNullOrEmpty(tenantId)) query = query.Where(c => c.TenantId == tenantId);
        
        return Task.FromResult<IReadOnlyList<DataConnectorConfig>>(query.ToList());
    }

    public Task DeleteAsync(string id, CancellationToken ct = default)
    {
        _connectors.TryRemove(id, out _);
        return Task.CompletedTask;
    }
}
