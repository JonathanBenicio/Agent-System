using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Interfaces;

/// <summary>
/// Persistence store for data connector configurations.
/// </summary>
public interface IDataConnectorStore
{
    Task<DataConnectorConfig> SaveAsync(DataConnectorConfig config, CancellationToken ct = default);
    Task<DataConnectorConfig?> GetAsync(string id, CancellationToken ct = default);
    Task<IReadOnlyList<DataConnectorConfig>> ListAsync(string? tenantId = null, CancellationToken ct = default);
    Task DeleteAsync(string id, CancellationToken ct = default);
}
