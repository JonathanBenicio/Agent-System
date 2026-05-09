using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Core.Services;
using AgenticSystem.Infrastructure.Documents;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace AgenticSystem.Tests;

public class DataConnectorTests
{
    private readonly IDataConnectorStore _store;
    private readonly IDocumentIngestionPipeline _ingestionPipeline;
    private readonly DataConnectorManager _manager;

    public DataConnectorTests()
    {
        _store = new InMemoryDataConnectorStore();
        _ingestionPipeline = Substitute.For<IDocumentIngestionPipeline>();
        
        var connectors = new List<IDataConnector>
        {
            new FileSystemDataConnector(_ingestionPipeline, Substitute.For<ILogger<FileSystemDataConnector>>())
        };

        _manager = new DataConnectorManager(
            _store,
            connectors,
            Substitute.For<ILogger<DataConnectorManager>>());
    }

    [Fact]
    public async Task RegisterConnectorAsync_ShouldVerifyConnectionAndSave()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var config = new DataConnectorConfig
            {
                Id = "conn-1",
                Name = "Local Docs",
                ConnectorType = DataConnectorType.FileSystem,
                ConnectionString = tempDir
            };

            // Act
            var result = await _manager.RegisterConnectorAsync(config);

            // Assert
            result.Status.Should().Be(ConnectorStatus.Configured);
            var saved = await _store.GetAsync("conn-1");
            saved.Should().NotBeNull();
            saved!.Name.Should().Be("Local Docs");
        }
        finally
        {
            Directory.Delete(tempDir);
        }
    }

    [Fact]
    public async Task SyncConnectorAsync_ShouldIngestFiles()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var testFile = Path.Combine(tempDir, "test.txt");
        await File.WriteAllTextAsync(testFile, "Hello World");

        try
        {
            var config = new DataConnectorConfig
            {
                Id = "sync-1",
                Name = "Sync Test",
                ConnectorType = DataConnectorType.FileSystem,
                ConnectionString = tempDir,
                TenantId = "tenant-1"
            };
            await _store.SaveAsync(config);

            _ingestionPipeline.IngestAsync(Arg.Any<RawDocument>(), Arg.Any<ChunkingConfig>())
                .Returns(IngestionResult.Ok("doc-1", "test.txt", 1, 10, "hash", TimeSpan.Zero));

            // Act
            var result = await _manager.SyncConnectorAsync("sync-1");

            // Assert
            result.Success.Should().BeTrue();
            result.DocumentsSynced.Should().Be(1);
            await _ingestionPipeline.Received(1).IngestAsync(
                Arg.Is<RawDocument>(d => d.FileName == "test.txt"), 
                Arg.Is<ChunkingConfig>(c => c.TenantId == "tenant-1"));
        }
        finally
        {
            File.Delete(testFile);
            Directory.Delete(tempDir);
        }
    }
}
