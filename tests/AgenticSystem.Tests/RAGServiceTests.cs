using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.RAG;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace AgenticSystem.Tests;

public class RAGServiceTests
{
    private readonly IVectorStore _vectorStore;
    private readonly IReRanker _reRanker;
    private readonly RAGService _ragService;

    public RAGServiceTests()
    {
        _vectorStore = Substitute.For<IVectorStore>();
        _reRanker = Substitute.For<IReRanker>();
        _ragService = new RAGService(_vectorStore, _reRanker, Substitute.For<ILogger<RAGService>>());
    }

    [Fact]
    public async Task RetrieveContextAsync_NoResults_ShouldReturnEmptyContext()
    {
        _vectorStore.SearchAsync(Arg.Any<string>(), Arg.Any<SearchScope>(), Arg.Any<int>())
            .Returns(new SearchResult { Matches = new List<SearchMatch>(), TotalFound = 0, Query = "test" });

        var query = new RAGQuery { Query = "test" };
        var result = await _ragService.RetrieveContextAsync(query);

        result.Chunks.Should().BeEmpty();
        result.BuiltContext.Should().BeNullOrEmpty();
        result.CandidatesRetrieved.Should().Be(0);
    }

    [Fact]
    public async Task RetrieveContextAsync_WithResults_ShouldBuildContext()
    {
        var matches = new List<SearchMatch>
        {
            new() { Id = "1", Content = "Relevant info about architecture.", Score = 0.9,
                Metadata = new Dictionary<string, string> { ["source"] = "docs" } },
            new() { Id = "2", Content = "More info about patterns.", Score = 0.7,
                Metadata = new Dictionary<string, string> { ["source"] = "notes" } }
        };

        _vectorStore.SearchAsync(Arg.Any<string>(), Arg.Any<SearchScope>(), Arg.Any<int>())
            .Returns(new SearchResult { Matches = matches, TotalFound = 2, Query = "architecture" });

        var rankedChunks = new List<RankedChunk>
        {
            new() { Id = "1", Content = "Relevant info about architecture.", Rank = 1,
                OriginalScore = 0.9, ReRankedScore = 0.95, Source = "docs" }
        };
        _reRanker.ReRankAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<SearchMatch>>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<RankedChunk>>(rankedChunks));

        var query = new RAGQuery { Query = "architecture", TopKAfterReRank = 1 };
        var result = await _ragService.RetrieveContextAsync(query);

        result.Chunks.Should().HaveCount(1);
        result.BuiltContext.Should().Contain("Relevant info about architecture");
        result.CandidatesRetrieved.Should().Be(2);
        result.CandidatesAfterReRank.Should().Be(1);
        result.StrategyUsed.Should().Be(RetrievalStrategy.Default);
    }

    [Fact]
    public async Task RetrieveContextAsync_DomainStrategy_ShouldApplyFilter()
    {
        _vectorStore.SearchWithFiltersAsync(Arg.Any<string>(), Arg.Any<Dictionary<string, string>>())
            .Returns(new SearchResult { Matches = new List<SearchMatch>(), TotalFound = 0, Query = "test" });

        var query = new RAGQuery { Query = "domain concepts", Strategy = RetrievalStrategy.DomainKnowledge };
        await _ragService.RetrieveContextAsync(query);

        await _vectorStore.Received(1).SearchWithFiltersAsync(
            Arg.Any<string>(),
            Arg.Is<Dictionary<string, string>>(f => f.ContainsKey("content_type") && f["content_type"] == "domain"));
    }

    [Fact]
    public async Task RetrieveContextAsync_DecisionStrategy_ShouldApplyFilter()
    {
        _vectorStore.SearchWithFiltersAsync(Arg.Any<string>(), Arg.Any<Dictionary<string, string>>())
            .Returns(new SearchResult { Matches = new List<SearchMatch>(), TotalFound = 0, Query = "test" });

        var query = new RAGQuery { Query = "past decisions", Strategy = RetrievalStrategy.DecisionHistory };
        await _ragService.RetrieveContextAsync(query);

        await _vectorStore.Received(1).SearchWithFiltersAsync(
            Arg.Any<string>(),
            Arg.Is<Dictionary<string, string>>(f => f["content_type"] == "decision"));
    }

    [Fact]
    public async Task RetrieveContextAsync_ShouldFilterBelowMinScore()
    {
        var matches = new List<SearchMatch>
        {
            new() { Id = "1", Content = "High relevance.", Score = 0.9,
                Metadata = new Dictionary<string, string>() },
            new() { Id = "2", Content = "Low relevance.", Score = 0.1,
                Metadata = new Dictionary<string, string>() }
        };

        _vectorStore.SearchAsync(Arg.Any<string>(), Arg.Any<SearchScope>(), Arg.Any<int>())
            .Returns(new SearchResult { Matches = matches, TotalFound = 2, Query = "test" });

        _reRanker.ReRankAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<SearchMatch>>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var candidates = callInfo.ArgAt<IReadOnlyList<SearchMatch>>(1);
                var ranked = candidates.Select((m, i) => new RankedChunk
                {
                    Id = m.Id, Content = m.Content, Rank = i + 1,
                    OriginalScore = m.Score, ReRankedScore = m.Score
                }).ToList();
                return Task.FromResult<IReadOnlyList<RankedChunk>>(ranked);
            });

        var query = new RAGQuery { Query = "test", MinRelevanceScore = 0.5 };
        var result = await _ragService.RetrieveContextAsync(query);

        // Only the high-relevance match should reach the reranker
        await _reRanker.Received(1).ReRankAsync(
            Arg.Any<string>(),
            Arg.Is<IReadOnlyList<SearchMatch>>(m => m.Count == 1),
            Arg.Any<int>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RetrieveContextAsync_WithAgentId_ShouldIncludeInFilters()
    {
        _vectorStore.SearchWithFiltersAsync(Arg.Any<string>(), Arg.Any<Dictionary<string, string>>())
            .Returns(new SearchResult { Matches = new List<SearchMatch>(), TotalFound = 0, Query = "test" });

        var query = new RAGQuery
        {
            Query = "test",
            AgentId = "agent-123",
            Strategy = RetrievalStrategy.DomainKnowledge
        };
        await _ragService.RetrieveContextAsync(query);

        await _vectorStore.Received(1).SearchWithFiltersAsync(
            Arg.Any<string>(),
            Arg.Is<Dictionary<string, string>>(f => f["agent_id"] == "agent-123"));
    }

    [Fact]
    public async Task RetrieveContextAsync_ShouldMeasureTiming()
    {
        _vectorStore.SearchAsync(Arg.Any<string>(), Arg.Any<SearchScope>(), Arg.Any<int>())
            .Returns(new SearchResult { Matches = new List<SearchMatch>(), TotalFound = 0, Query = "test" });

        var query = new RAGQuery { Query = "test" };
        var result = await _ragService.RetrieveContextAsync(query);

        result.TotalTime.Should().BeGreaterOrEqualTo(TimeSpan.Zero);
        result.RetrievalTime.Should().BeGreaterOrEqualTo(TimeSpan.Zero);
    }
}
