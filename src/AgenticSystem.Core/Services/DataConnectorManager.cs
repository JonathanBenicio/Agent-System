using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Core.Services;

public class DataConnectorManager : IDataConnectorManager
{
    private readonly IDataConnectorStore _store;
    private readonly IEnumerable<IDataConnector> _connectors;
    private readonly ILogger<DataConnectorManager> _logger;

    public DataConnectorManager(
        IDataConnectorStore store,
        IEnumerable<IDataConnector> connectors,
        ILogger<DataConnectorManager> logger)
    {
        _store = store;
        _connectors = connectors;
        _logger = logger;
    }

    public async Task<DataConnectorConfig> RegisterConnectorAsync(DataConnectorConfig config, CancellationToken ct = default)
    {
        _logger.LogInformation("🔌 Registering data connector: {Name} (Type: {Type})", config.Name, config.ConnectorType);
        
        var connector = GetConnector(config.ConnectorType);
        bool connected = await connector.TestConnectionAsync(config, ct);
        
        if (!connected)
        {
            _logger.LogWarning("⚠️ Could not establish connection for {Name}. Status set to Error.", config.Name);
            config.Status = ConnectorStatus.Error;
        }
        else
        {
            config.Status = ConnectorStatus.Configured;
        }

        return await _store.SaveAsync(config, ct);
    }

    public async Task<DataSyncResult> SyncConnectorAsync(string connectorId, bool fullSync = false, CancellationToken ct = default)
    {
        var config = await _store.GetAsync(connectorId, ct);
        if (config == null) throw new ArgumentException($"Connector {connectorId} not found.");

        _logger.LogInformation("🔄 Starting sync for connector: {Name}", config.Name);
        
        config.Status = ConnectorStatus.Syncing;
        await _store.SaveAsync(config, ct);

        var connector = GetConnector(config.ConnectorType);
        var result = await connector.SyncAsync(config, fullSync, ct);

        config.LastSyncAt = DateTime.UtcNow;
        config.Status = result.Success ? ConnectorStatus.Ready : ConnectorStatus.Error;
        await _store.SaveAsync(config, ct);

        return result;
    }

    public Task<IReadOnlyList<DataConnectorConfig>> ListConnectorsAsync(string? tenantId = null, CancellationToken ct = default)
    {
        return _store.ListAsync(tenantId, ct);
    }

    public Task RemoveConnectorAsync(string connectorId, CancellationToken ct = default)
    {
        return _store.DeleteAsync(connectorId, ct);
    }

    private IDataConnector GetConnector(DataConnectorType type)
    {
        var connector = _connectors.FirstOrDefault(c => c.ConnectorType == type);
        if (connector == null) throw new NotSupportedException($"No connector implementation found for type {type}.");
        return connector;
    }
}
