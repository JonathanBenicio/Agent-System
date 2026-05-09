using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.RAG;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace AgenticSystem.Tests;

public class AdvancedRetrievalTests
{
    private readonly IVectorStore _vectorStore;
    private readonly IReRanker _reRanker;
    private readonly IAdvancedRetrievalService _advancedRetrieval;
    private readonly RAGService _ragService;

    public AdvancedRetrievalTests()
    {
        _vectorStore = Substitute.For<IVectorStore>();
        _reRanker = Substitute.For<IReRanker>();
        _advancedRetrieval = Substitute.For<IAdvancedRetrievalService>();

        _ragService = new RAGService(
            _vectorStore,
            _reRanker,
            Substitute.For<ILogger<RAGService>>(),
            advancedRetrievalService: _advancedRetrieval);
    }

    [Fact]
    public async Task RetrieveContextAsync_WithHybridSearchFlag_ShouldCallAdvancedRetrieval()
    {
        // Arrange
        var query = new RAGQuery
        {
            Query = "test query",
            UseHybridSearch = true
        };

        var mockChunks = new List<RankedChunk>
        {
            new() { Id = "chunk-1", Content = "Advanced content", OriginalScore = 0.95 }
        };

        _advancedRetrieval.HybridSearchAsync(Arg.Is<RAGQuery>(q => q.Query == "test query"), null)
            .Returns(new RAGContext
            {
                Query = "test query",
                EffectiveQuery = "test query",
                Chunks = mockChunks,
                CandidatesRetrieved = 1
            });

        _reRanker.ReRankAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<SearchMatch>>(), Arg.Any<int>())
            .Returns(mockChunks);

        // Act
        var result = await _ragService.RetrieveContextAsync(query);

        // Assert
        result.Chunks.Should().HaveCount(1);
        result.Chunks[0].Content.Should().Be("Advanced content");
        await _advancedRetrieval.Received(1).HybridSearchAsync(Arg.Any<RAGQuery>(), null);
        await _vectorStore.DidNotReceiveWithAnyArgs().SearchAsync(default!, default, default);
    }

    [Fact]
    public async Task RetrieveContextAsync_WithMultiQueryFlag_ShouldCallAdvancedMultiQuery()
    {
        // Arrange
        var query = new RAGQuery
        {
            Query = "complex question",
            UseMultiQuery = true
        };

        var mockChunks = new List<RankedChunk>
        {
            new() { Id = "chunk-1", Content = "MQ content", OriginalScore = 0.88 }
        };

        _advancedRetrieval.MultiQueryRetrieveAsync(Arg.Is<RAGQuery>(q => q.Query == "complex question"))
            .Returns(new RAGContext
            {
                Query = "complex question",
                EffectiveQuery = "complex question",
                QueryVariants = new List<string> { "sub-1", "sub-2" },
                Chunks = mockChunks,
                CandidatesRetrieved = 5
            });

        _reRanker.ReRankAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<SearchMatch>>(), Arg.Any<int>())
            .Returns(mockChunks);

        // Act
        var result = await _ragService.RetrieveContextAsync(query);

        // Assert
        result.Chunks.Should().HaveCount(1);
        result.QueryVariants.Should().Contain("sub-1");
        await _advancedRetrieval.Received(1).MultiQueryRetrieveAsync(Arg.Any<RAGQuery>());
    }

    [Fact]
    public async Task RetrieveContextAsync_WithSelfCorrectionFlag_ShouldCallAdvancedSelfCorrection()
    {
        // Arrange
        var query = new RAGQuery
        {
            Query = "risky query",
            UseSelfCorrection = true
        };

        _advancedRetrieval.SelfCorrectiveRetrieveAsync(Arg.Any<RAGQuery>())
            .Returns(new RAGContext
            {
                Query = "risky query",
                Chunks = new List<RankedChunk> { new() { Id = "c1", Content = "Corrected" } }
            });

        _reRanker.ReRankAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<SearchMatch>>(), Arg.Any<int>())
            .Returns(new List<RankedChunk> { new() { Id = "c1", Content = "Corrected" } });

        // Act
        await _ragService.RetrieveContextAsync(query);

        // Assert
        await _advancedRetrieval.Received(1).SelfCorrectiveRetrieveAsync(Arg.Any<RAGQuery>());
    }
}
