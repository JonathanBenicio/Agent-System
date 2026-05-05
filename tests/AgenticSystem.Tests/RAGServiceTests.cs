using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.RAG;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Tests;

public partial class RAGServiceTests
{
    [Fact]
    public async Task RetrieveContextAsync_WhenCompressionGeneratesVariant_MergesDistinctMatchesBeforeReRanking()
    {
        // Arrange
        var vectorStore = Substitute.For<IVectorStore>();
        var reRanker = Substitute.For<IReRanker>();
        var logger = Substitute.For<ILogger<RAGService>>();
        var queryCompressor = Substitute.For<IQueryCompressor>();

        queryCompressor
            .CompressAsync(Arg.Any<string>(), Arg.Any<QueryCompressionStrategy>())
            .Returns(new CompressedQuery
            {
                OriginalQuery = "how to fix runtime error in sdk",
                CompressedText = "sdk runtime error",
                ExtractedKeyTerms = new List<string> { "sdk", "runtime", "error" }
            });

        vectorStore
            .SearchAsync("sdk runtime error", SearchScope.All, 10)
            .Returns(new SearchResult
            {
                Matches = new List<SearchMatch>
                {
                    new() { Id = "chunk-1", Content = "SDK runtime troubleshooting guide", Score = 0.92, Metadata = new Dictionary<string, string> { ["source"] = "guide" } },
                    new() { Id = "chunk-2", Content = "Known runtime errors in SDK", Score = 0.85, Metadata = new Dictionary<string, string> { ["source"] = "kb" } }
                }
            });

        vectorStore
            .SearchAsync("how to fix runtime error in sdk", SearchScope.All, 10)
            .Returns(new SearchResult
            {
                Matches = new List<SearchMatch>
                {
                    new() { Id = "chunk-1", Content = "SDK runtime troubleshooting guide", Score = 0.70, Metadata = new Dictionary<string, string> { ["source"] = "guide" } }
                }
            });

        IReadOnlyList<SearchMatch>? receivedCandidates = null;
        string? receivedQuery = null;
        reRanker
            .ReRankAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<SearchMatch>>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                receivedQuery = callInfo.ArgAt<string>(0);
                receivedCandidates = callInfo.ArgAt<IReadOnlyList<SearchMatch>>(1);

                var ranked = receivedCandidates
                    .OrderByDescending(match => match.Score)
                    .Select((match, index) => new RankedChunk
                    {
                        Id = match.Id,
                        Content = match.Content,
                        OriginalScore = match.Score,
                        ReRankedScore = match.Score,
                        Rank = index + 1,
                        Source = match.Metadata.GetValueOrDefault("source", string.Empty),
                        Metadata = match.Metadata
                    })
                    .ToList();

                return Task.FromResult<IReadOnlyList<RankedChunk>>(ranked);
            });

        var service = new RAGService(vectorStore, reRanker, logger, queryCompressor: queryCompressor);

        // Act
        var result = await service.RetrieveContextAsync(new RAGQuery
        {
            Query = "how to fix runtime error in sdk"
        });

        // Assert
        receivedQuery.Should().Be("how to fix runtime error in sdk");
        receivedCandidates.Should().NotBeNull();
        receivedCandidates!.Should().HaveCount(2);
        result.QueryVariants.Should().Contain(new[] { "sdk runtime error", "how to fix runtime error in sdk" });
        result.EffectiveQuery.Should().Be("sdk runtime error");
    }

    [Fact]
    public async Task RetrieveContextAsync_WhenContextIsLarge_UsesSemanticCompressionToReducePromptContext()
    {
        // Arrange
        var vectorStore = Substitute.For<IVectorStore>();
        var reRanker = Substitute.For<IReRanker>();
        var logger = Substitute.For<ILogger<RAGService>>();
        var semanticCompressor = Substitute.For<ISemanticCompressor>();

        var longContent = string.Join(' ', Enumerable.Repeat("runtime diagnostics for sdk failures and mitigation", 40));
        var matches = Enumerable.Range(1, 4)
            .Select(index => new SearchMatch
            {
                Id = $"chunk-{index}",
                Content = $"Section {index}. {longContent}",
                Score = 0.90 - (index * 0.05),
                Metadata = new Dictionary<string, string>
                {
                    ["source"] = $"doc-{index}",
                    ["section"] = $"section-{index}"
                }
            })
            .ToList();

        vectorStore
            .SearchAsync("sdk runtime diagnostics", SearchScope.All, 10)
            .Returns(new SearchResult { Matches = matches });

        reRanker
            .ReRankAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<SearchMatch>>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var candidates = callInfo.ArgAt<IReadOnlyList<SearchMatch>>(1);
                var ranked = candidates.Select((match, index) => new RankedChunk
                {
                    Id = match.Id,
                    Content = match.Content,
                    OriginalScore = match.Score,
                    ReRankedScore = match.Score,
                    Rank = index + 1,
                    Source = match.Metadata.GetValueOrDefault("source", string.Empty),
                    Section = match.Metadata.GetValueOrDefault("section", string.Empty),
                    Metadata = match.Metadata
                }).ToList();

                return Task.FromResult<IReadOnlyList<RankedChunk>>(ranked);
            });

        semanticCompressor
            .CompressRankedChunksAsync(Arg.Any<IEnumerable<RankedChunk>>(), "sdk runtime diagnostics")
            .Returns(new SemanticSummary
            {
                CompressedKnowledge = "Tema: sdk runtime diagnostics\n- Diagnóstico consolidado das falhas mais frequentes.\n- Mitigações recorrentes por seção.",
                OriginalTokenCount = 600,
                CompressedTokenCount = 40,
                CompressionRatio = 0.07
            });

        var service = new RAGService(vectorStore, reRanker, logger, semanticCompressor: semanticCompressor);

        // Act
        var result = await service.RetrieveContextAsync(new RAGQuery
        {
            Query = "sdk runtime diagnostics"
        });

        // Assert
        result.UsedSemanticCompression.Should().BeTrue();
        result.SemanticSummary.Should().Contain("Diagnóstico consolidado");
        result.OriginalContextTokens.Should().BeGreaterThan(result.TotalTokensUsed);
        result.BuiltContext.Should().Contain("### Semantic Summary");
    }
}

public partial class RAGServiceTests
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
