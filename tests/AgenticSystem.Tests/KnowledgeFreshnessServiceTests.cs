using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using AgenticSystem.Core.Models;
using AgenticSystem.Core.Services;

namespace AgenticSystem.Tests;

public class KnowledgeFreshnessServiceTests
{
    private readonly KnowledgeFreshnessService _sut;

    public KnowledgeFreshnessServiceTests()
    {
        var logger = Substitute.For<ILogger<KnowledgeFreshnessService>>();
        _sut = new KnowledgeFreshnessService(logger);
    }

    [Fact]
    public async Task GetFreshnessAsync_NewDocument_ReturnsFreshScore()
    {
        var freshness = await _sut.GetFreshnessAsync("doc-1");

        freshness.DocumentId.Should().Be("doc-1");
        freshness.FreshnessScore.Should().Be(1.0);
    }

    [Fact]
    public async Task SetValidityPeriodAsync_SetsExpirationDate()
    {
        await _sut.GetFreshnessAsync("doc-2");
        var validity = TimeSpan.FromDays(30);

        await _sut.SetValidityPeriodAsync("doc-2", validity);

        var freshness = await _sut.GetFreshnessAsync("doc-2");
        freshness.ValidityPeriod.Should().Be(validity);
        freshness.ExpiresAt.Should().NotBeNull();
    }

    [Fact]
    public async Task DetectDriftAsync_OldDocument_DetectsDrift()
    {
        var freshness = await _sut.GetFreshnessAsync("doc-old");
        freshness.ContentDate = DateTime.UtcNow.AddDays(-120);
        freshness.LastVerifiedAt = DateTime.UtcNow.AddDays(-60);

        var report = await _sut.DetectDriftAsync("doc-old");

        report.HasDrift.Should().BeTrue();
        report.DriftScore.Should().BeGreaterThan(0.3);
        report.DriftIndicators.Should().NotBeEmpty();
    }

    [Fact]
    public async Task DetectDriftAsync_FreshDocument_NoDrift()
    {
        var freshness = await _sut.GetFreshnessAsync("doc-fresh");
        freshness.ContentDate = DateTime.UtcNow;
        freshness.LastVerifiedAt = DateTime.UtcNow;

        var report = await _sut.DetectDriftAsync("doc-fresh");

        report.HasDrift.Should().BeFalse();
        report.DriftScore.Should().BeLessThan(0.3);
    }

    [Fact]
    public async Task MarkVerifiedAsync_ResetsFreshness()
    {
        var freshness = await _sut.GetFreshnessAsync("doc-v");
        freshness.FreshnessScore = 0.3;
        freshness.IsPotentiallyStale = true;

        await _sut.MarkVerifiedAsync("doc-v");

        freshness.FreshnessScore.Should().Be(1.0);
        freshness.IsPotentiallyStale.Should().BeFalse();
    }

    [Fact]
    public async Task GetStaleDocumentsAsync_ReturnsOnlyStale()
    {
        var fresh = await _sut.GetFreshnessAsync("doc-ok");
        fresh.IsPotentiallyStale = false;

        var stale = await _sut.GetFreshnessAsync("doc-stale");
        stale.IsPotentiallyStale = true;

        var results = await _sut.GetStaleDocumentsAsync();

        results.Should().Contain(f => f.DocumentId == "doc-stale");
        results.Should().NotContain(f => f.DocumentId == "doc-ok");
    }

    [Fact]
    public async Task CalculateFreshnessScoreAsync_ReturnsScoreBetween0And1()
    {
        await _sut.GetFreshnessAsync("doc-calc");

        var score = await _sut.CalculateFreshnessScoreAsync("doc-calc");

        score.Should().BeInRange(0.0, 1.0);
    }
}
