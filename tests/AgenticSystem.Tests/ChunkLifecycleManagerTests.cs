using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using AgenticSystem.Core.Models;
using AgenticSystem.Core.Services;

namespace AgenticSystem.Tests;

public class ChunkLifecycleManagerTests
{
    private readonly ChunkLifecycleManager _sut;

    public ChunkLifecycleManagerTests()
    {
        var logger = Substitute.For<ILogger<ChunkLifecycleManager>>();
        _sut = new ChunkLifecycleManager(logger);
    }

    [Fact]
    public async Task GetLifecycleAsync_NewChunk_ReturnsNewState()
    {
        var lifecycle = await _sut.GetLifecycleAsync("chunk-1");

        lifecycle.ChunkId.Should().Be("chunk-1");
        lifecycle.State.Should().Be(ChunkLifecycleState.New);
        lifecycle.FreshnessScore.Should().Be(1.0);
    }

    [Fact]
    public async Task RecordAccessAsync_ThreeAccesses_PromotesToActive()
    {
        await _sut.GetLifecycleAsync("chunk-2");

        await _sut.RecordAccessAsync("chunk-2");
        await _sut.RecordAccessAsync("chunk-2");
        await _sut.RecordAccessAsync("chunk-2");

        var lifecycle = await _sut.GetLifecycleAsync("chunk-2");
        lifecycle.State.Should().Be(ChunkLifecycleState.Active);
        lifecycle.AccessCount.Should().Be(3);
    }

    [Fact]
    public async Task PromoteAsync_NewToActive()
    {
        await _sut.GetLifecycleAsync("chunk-3");
        await _sut.PromoteAsync("chunk-3");

        var lifecycle = await _sut.GetLifecycleAsync("chunk-3");
        lifecycle.State.Should().Be(ChunkLifecycleState.Active);
    }

    [Fact]
    public async Task ArchiveAsync_SetsArchivedState()
    {
        await _sut.GetLifecycleAsync("chunk-4");
        await _sut.ArchiveAsync("chunk-4");

        var lifecycle = await _sut.GetLifecycleAsync("chunk-4");
        lifecycle.State.Should().Be(ChunkLifecycleState.Archived);
        lifecycle.ArchivedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task GetStaleChunksAsync_ReturnsOldChunks()
    {
        var lifecycle = await _sut.GetLifecycleAsync("stale-1");
        lifecycle.LastAccessedAt = DateTime.UtcNow.AddDays(-5);

        var stale = await _sut.GetStaleChunksAsync(TimeSpan.FromDays(1));
        stale.Should().Contain(l => l.ChunkId == "stale-1");
    }

    [Fact]
    public async Task ConsolidateChunksAsync_MarksSourcesAndCreatesTarget()
    {
        await _sut.GetLifecycleAsync("src-1");
        await _sut.GetLifecycleAsync("src-2");

        await _sut.ConsolidateChunksAsync(new[] { "src-1", "src-2" }, "target-1");

        var src1 = await _sut.GetLifecycleAsync("src-1");
        src1.State.Should().Be(ChunkLifecycleState.Consolidated);
        src1.ConsolidatedIntoId.Should().Be("target-1");

        var target = await _sut.GetLifecycleAsync("target-1");
        target.State.Should().Be(ChunkLifecycleState.Active);
    }

    [Fact]
    public async Task ApplyDecayAsync_ReducesFreshnessForOldChunks()
    {
        var lifecycle = await _sut.GetLifecycleAsync("decay-1");
        await _sut.PromoteAsync("decay-1");
        lifecycle.LastAccessedAt = DateTime.UtcNow.AddHours(-100);

        await _sut.ApplyDecayAsync();

        lifecycle.FreshnessScore.Should().BeLessThan(1.0);
    }
}
