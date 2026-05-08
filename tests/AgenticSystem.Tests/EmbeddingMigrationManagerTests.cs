using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Core.Services;

namespace AgenticSystem.Tests;

public class EmbeddingMigrationManagerTests
{
    private readonly InMemoryEmbeddingModelStore _modelStore;
    private readonly InMemoryMigrationJobStore _jobStore;
    private readonly IEmbeddingGenerator _generator;
    private readonly IVectorStore _vectorStore;
    private readonly EmbeddingMigrationManager _sut;

    public EmbeddingMigrationManagerTests()
    {
        _modelStore = new InMemoryEmbeddingModelStore();
        _jobStore = new InMemoryMigrationJobStore();
        _generator = Substitute.For<IEmbeddingGenerator>();
        _vectorStore = Substitute.For<IVectorStore>();
        var logger = Substitute.For<ILogger<EmbeddingMigrationManager>>();
        _sut = new EmbeddingMigrationManager(_jobStore, _modelStore, _vectorStore, _generator, logger);
    }

    private async Task<EmbeddingModelConfig> RegisterModelAsync(EmbeddingModelConfig model)
    {
        await _modelStore.SaveAsync(model);
        return model;
    }

    [Fact]
    public async Task StartMigrationAsync_CreatesJob()
    {
        var source = await RegisterModelAsync(new EmbeddingModelConfig { Provider = EmbeddingProvider.OpenAI, ModelName = "old", Dimensions = 768, ApiKey = "k" });
        var target = await RegisterModelAsync(new EmbeddingModelConfig { Provider = EmbeddingProvider.OpenAI, ModelName = "new", Dimensions = 1536, ApiKey = "k" });

        _vectorStore.SearchAsync("*", Arg.Any<SearchScope>(), Arg.Any<int>())
            .Returns(new SearchResult { Matches = new List<SearchMatch>(), TotalFound = 0, Query = "*" });

        var request = new StartMigrationRequest
        {
            SourceModelId = source.Id,
            TargetModelId = target.Id,
            SourceCollection = "default"
        };

        var job = await _sut.StartMigrationAsync(request);

        job.Should().NotBeNull();
        job.Status.Should().Be(MigrationStatus.Pending);
        job.SourceCollection.Should().Be("default");
    }

    [Fact]
    public async Task CancelAsync_SetsStatusCancelled()
    {
        var source = await RegisterModelAsync(new EmbeddingModelConfig { Provider = EmbeddingProvider.OpenAI, ModelName = "s", Dimensions = 768, ApiKey = "k" });
        var target = await RegisterModelAsync(new EmbeddingModelConfig { Provider = EmbeddingProvider.OpenAI, ModelName = "t", Dimensions = 1536, ApiKey = "k" });

        _vectorStore.SearchAsync("*", Arg.Any<SearchScope>(), Arg.Any<int>())
            .Returns(async callInfo => 
            {
                await Task.Delay(1000); // Simulate long running search
                return new SearchResult { Matches = new List<SearchMatch>(), TotalFound = 0, Query = "*" };
            });

        var job = await _sut.StartMigrationAsync(new StartMigrationRequest { SourceModelId = source.Id, TargetModelId = target.Id });

        await _sut.CancelAsync(job.Id);

        var updated = await _jobStore.GetAsync(job.Id);
        updated!.Status.Should().Be(MigrationStatus.Cancelled);
    }

    [Fact]
    public async Task GetStatusAsync_ReturnsSummary()
    {
        var source = await RegisterModelAsync(new EmbeddingModelConfig { Provider = EmbeddingProvider.OpenAI, ModelName = "src", Dimensions = 768, ApiKey = "k" });
        var target = await RegisterModelAsync(new EmbeddingModelConfig { Provider = EmbeddingProvider.Google, ModelName = "tgt", Dimensions = 256, ApiKey = "k" });

        _vectorStore.SearchAsync("*", Arg.Any<SearchScope>(), Arg.Any<int>())
            .Returns(new SearchResult { Matches = new List<SearchMatch>(), TotalFound = 0, Query = "*" });

        var job = await _sut.StartMigrationAsync(new StartMigrationRequest { SourceModelId = source.Id, TargetModelId = target.Id });

        var status = await _sut.GetStatusAsync(job.Id);

        status.Should().NotBeNull();
        status.JobId.Should().Be(job.Id);
        status.SourceModel.Should().Contain("src");
        status.TargetModel.Should().Contain("tgt");
    }

    [Fact]
    public async Task GetJobAsync_ReturnsJob()
    {
        var source = await RegisterModelAsync(new EmbeddingModelConfig { Provider = EmbeddingProvider.Ollama, ModelName = "m1", Dimensions = 384, ApiKey = "" });
        var target = await RegisterModelAsync(new EmbeddingModelConfig { Provider = EmbeddingProvider.Ollama, ModelName = "m2", Dimensions = 768, ApiKey = "" });

        _vectorStore.SearchAsync("*", Arg.Any<SearchScope>(), Arg.Any<int>())
            .Returns(new SearchResult { Matches = new List<SearchMatch>(), TotalFound = 0, Query = "*" });

        var job = await _sut.StartMigrationAsync(new StartMigrationRequest { SourceModelId = source.Id, TargetModelId = target.Id });

        var retrieved = await _sut.GetJobAsync(job.Id);

        retrieved.Should().NotBeNull();
        retrieved!.Id.Should().Be(job.Id);
    }

    [Fact]
    public async Task GetAllJobsAsync_ReturnsAll()
    {
        var s = await RegisterModelAsync(new EmbeddingModelConfig { Provider = EmbeddingProvider.OpenAI, ModelName = "s", Dimensions = 768, ApiKey = "k" });
        var t = await RegisterModelAsync(new EmbeddingModelConfig { Provider = EmbeddingProvider.OpenAI, ModelName = "t", Dimensions = 1536, ApiKey = "k" });

        _vectorStore.SearchAsync("*", Arg.Any<SearchScope>(), Arg.Any<int>())
            .Returns(new SearchResult { Matches = new List<SearchMatch>(), TotalFound = 0, Query = "*" });

        await _sut.StartMigrationAsync(new StartMigrationRequest { SourceModelId = s.Id, TargetModelId = t.Id });
        await _sut.StartMigrationAsync(new StartMigrationRequest { SourceModelId = s.Id, TargetModelId = t.Id });

        var jobs = await _sut.GetAllJobsAsync();

        jobs.Should().HaveCount(2);
    }
}

public class InMemoryEmbeddingModelStoreTests
{
    private readonly InMemoryEmbeddingModelStore _sut = new();

    [Fact]
    public async Task SaveAsync_And_GetAllAsync()
    {
        var model = new EmbeddingModelConfig { Id = "m1", Provider = EmbeddingProvider.OpenAI, ModelName = "test", Dimensions = 768, ApiKey = "k" };

        await _sut.SaveAsync(model);
        var all = await _sut.GetAllAsync();

        all.Should().ContainSingle().Which.Id.Should().Be("m1");
    }

    [Fact]
    public async Task SetActiveAsync_DeactivatesOthers()
    {
        var m1 = new EmbeddingModelConfig { Id = "1", Provider = EmbeddingProvider.OpenAI, ModelName = "a", Dimensions = 768, ApiKey = "k", IsActive = true };
        var m2 = new EmbeddingModelConfig { Id = "2", Provider = EmbeddingProvider.Google, ModelName = "b", Dimensions = 256, ApiKey = "k" };
        await _sut.SaveAsync(m1);
        await _sut.SaveAsync(m2);

        await _sut.SetActiveAsync("2");

        var active = await _sut.GetActiveAsync();
        active.Id.Should().Be("2");
        var all = await _sut.GetAllAsync();
        all.Single(x => x.Id == "1").IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAsync_RemovesModel()
    {
        await _sut.SaveAsync(new EmbeddingModelConfig { Id = "del", Provider = EmbeddingProvider.Ollama, ModelName = "del", Dimensions = 384 });

        await _sut.DeleteAsync("del");

        var all = await _sut.GetAllAsync();
        all.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAsync_NonExistent_ReturnsNull()
    {
        var result = await _sut.GetAsync("nope");
        result.Should().BeNull();
    }
}
