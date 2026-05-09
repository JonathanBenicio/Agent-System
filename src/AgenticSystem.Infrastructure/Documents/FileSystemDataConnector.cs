using System.Diagnostics;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Infrastructure.Documents;

public class FileSystemDataConnector : IDataConnector
{
    private readonly IDocumentIngestionPipeline _ingestionPipeline;
    private readonly ILogger<FileSystemDataConnector> _logger;

    public FileSystemDataConnector(
        IDocumentIngestionPipeline ingestionPipeline,
        ILogger<FileSystemDataConnector> logger)
    {
        _ingestionPipeline = ingestionPipeline;
        _logger = logger;
    }

    public DataConnectorType ConnectorType => DataConnectorType.FileSystem;

    public Task<bool> TestConnectionAsync(DataConnectorConfig config, CancellationToken ct = default)
    {
        return Task.FromResult(Directory.Exists(config.ConnectionString));
    }

    public async Task<DataSyncResult> SyncAsync(DataConnectorConfig config, bool fullSync = false, CancellationToken ct = default)
    {
        _logger.LogInformation("📂 Syncing FileSystem connector: {Name} (Path: {Path})", config.Name, config.ConnectionString);
        
        var sw = Stopwatch.StartNew();
        var result = new DataSyncResult
        {
            ConnectorId = config.Id
        };

        if (!Directory.Exists(config.ConnectionString))
        {
            result.Success = false;
            result.ErrorMessage = $"Directory not found: {config.ConnectionString}";
            return result;
        }

        try
        {
            var files = Directory.GetFiles(config.ConnectionString, "*.*", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                if (ct.IsCancellationRequested) break;

                // Simple check for changed files
                var lastWrite = File.GetLastWriteTimeUtc(file);
                if (!fullSync && config.LastSyncAt.HasValue && lastWrite <= config.LastSyncAt.Value)
                {
                    continue;
                }

                _logger.LogDebug("📄 Processing file: {FileName}", Path.GetFileName(file));
                
                var content = await File.ReadAllBytesAsync(file, ct);
                var rawDoc = new RawDocument
                {
                    Id = Guid.NewGuid().ToString("N"),
                    FileName = Path.GetFileName(file),
                    Content = content
                };

                var ingestionResult = await _ingestionPipeline.IngestAsync(rawDoc, new ChunkingConfig
                {
                    Collection = config.Settings.GetValueOrDefault("collection", "default"),
                    TenantId = config.TenantId
                });

                if (ingestionResult.Success)
                {
                    result.DocumentsSynced++;
                }
                else
                {
                    result.Errors++;
                    result.ErrorMessage = ingestionResult.Error;
                }
            }

            result.Success = result.Errors == 0 || result.DocumentsSynced > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during FileSystem sync");
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }

        sw.Stop();
        result.Duration = sw.Elapsed;
        return result;
    }

    public Task<IReadOnlyList<string>> ListResourcesAsync(DataConnectorConfig config, string? path = null, CancellationToken ct = default)
    {
        var targetPath = string.IsNullOrEmpty(path) ? config.ConnectionString : Path.Combine(config.ConnectionString, path);
        if (!Directory.Exists(targetPath)) return Task.FromResult<IReadOnlyList<string>>(new List<string>());

        var entries = Directory.GetFileSystemEntries(targetPath)
            .Select(Path.GetFileName)
            .Where(f => f != null)
            .Cast<string>()
            .ToList();

        return Task.FromResult<IReadOnlyList<string>>(entries);
    }
}
