using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Interfaces;

/// <summary>
/// Unified data connector interface for external source integration.
/// </summary>
public interface IDataConnector
{
    /// <summary>
    /// The connector type this implementation handles.
    /// </summary>
    DataConnectorType ConnectorType { get; }

    /// <summary>
    /// Tests the connection to the external source.
    /// </summary>
    Task<bool> TestConnectionAsync(
        DataConnectorConfig config,
        CancellationToken ct = default);

    /// <summary>
    /// Synchronizes data from the external source.
    /// </summary>
    Task<DataSyncResult> SyncAsync(
        DataConnectorConfig config,
        bool fullSync = false,
        CancellationToken ct = default);

    /// <summary>
    /// Lists available resources/files from the source.
    /// </summary>
    Task<IReadOnlyList<string>> ListResourcesAsync(
        DataConnectorConfig config,
        string? path = null,
        CancellationToken ct = default);
}

/// <summary>
/// Manager for multiple data connectors.
/// </summary>
public interface IDataConnectorManager
{
    /// <summary>
    /// Registers a new data connector.
    /// </summary>
    Task<DataConnectorConfig> RegisterConnectorAsync(
        DataConnectorConfig config,
        CancellationToken ct = default);

    /// <summary>
    /// Triggers sync for a specific connector.
    /// </summary>
    Task<DataSyncResult> SyncConnectorAsync(
        string connectorId,
        bool fullSync = false,
        CancellationToken ct = default);

    /// <summary>
    /// Lists all registered connectors.
    /// </summary>
    Task<IReadOnlyList<DataConnectorConfig>> ListConnectorsAsync(
        string? tenantId = null,
        CancellationToken ct = default);

    /// <summary>
    /// Removes a connector.
    /// </summary>
    Task RemoveConnectorAsync(string connectorId, CancellationToken ct = default);
}
