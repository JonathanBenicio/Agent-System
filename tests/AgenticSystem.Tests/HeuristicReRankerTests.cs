using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.RAG;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace AgenticSystem.Tests;

public class HeuristicReRankerTests
{
    private readonly HeuristicReRanker _reRanker;

    public HeuristicReRankerTests()
    {
        _reRanker = new HeuristicReRanker(Substitute.For<ILogger<HeuristicReRanker>>());
    }

    [Fact]
    public async Task ReRankAsync_ShouldReturnTopKResults()
    {
        var candidates = CreateCandidates(10);

        var result = await _reRanker.ReRankAsync("test query", candidates, topK: 3);

        result.Should().HaveCount(3);
        result.Select(r => r.Rank).Should().BeEquivalentTo(new[] { 1, 2, 3 });
    }

    [Fact]
    public async Task ReRankAsync_ShouldBoostExactPhraseMatch()
    {
        var candidates = new List<SearchMatch>
        {
            new() { Id = "1", Content = "Unrelated content about cats.", Score = 0.7,
                Metadata = new Dictionary<string, string>() },
            new() { Id = "2", Content = "This is about test query details.", Score = 0.5,
                Metadata = new Dictionary<string, string>() }
        };

        var result = await _reRanker.ReRankAsync("test query", candidates, topK: 2);

        // The one with the exact phrase "test query" should rank higher despite lower original score
        result[0].Id.Should().Be("2");
    }

    [Fact]
    public async Task ReRankAsync_ShouldAssignRanksInOrder()
    {
        var candidates = CreateCandidates(5);

        var result = await _reRanker.ReRankAsync("sample content", candidates, topK: 5);

        result.Should().BeInAscendingOrder(r => r.Rank);
        result.Should().BeInDescendingOrder(r => r.ReRankedScore);
    }

    [Fact]
    public async Task ReRankAsync_EmptyCandidates_ShouldReturnEmpty()
    {
        var result = await _reRanker.ReRankAsync("test", new List<SearchMatch>(), topK: 5);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ReRankAsync_ShouldPreserveOriginalScore()
    {
        var candidates = new List<SearchMatch>
        {
            new() { Id = "1", Content = "Some content", Score = 0.85,
                Metadata = new Dictionary<string, string>() }
        };

        var result = await _reRanker.ReRankAsync("content", candidates, topK: 1);

        result[0].OriginalScore.Should().Be(0.85);
    }

    [Fact]
    public async Task ReRankAsync_ShouldBoostMetadataSectionMatch()
    {
        var candidates = new List<SearchMatch>
        {
            new() { Id = "1", Content = "Generic content paragraph.", Score = 0.6,
                Metadata = new Dictionary<string, string> { ["section"] = "Unrelated" } },
            new() { Id = "2", Content = "Generic content paragraph.", Score = 0.6,
                Metadata = new Dictionary<string, string> { ["section"] = "Architecture design" } }
        };

        var result = await _reRanker.ReRankAsync("architecture", candidates, topK: 2);

        result[0].Id.Should().Be("2");
    }

    private static List<SearchMatch> CreateCandidates(int count)
    {
        return Enumerable.Range(1, count)
            .Select(i => new SearchMatch
            {
                Id = i.ToString(),
                Content = $"Sample content number {i} with some text.",
                Score = 0.9 - (i * 0.05),
                Metadata = new Dictionary<string, string>
                {
                    ["source"] = "test",
                    ["section"] = $"Section {i}"
                }
            })
            .ToList();
    }
}
