using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Core.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace AgenticSystem.Tests;

public class MemoryInjectionServiceTests
{
    private readonly IVectorStore _vectorStore;
    private readonly ILogger<MemoryInjectionService> _logger;
    private readonly MemoryInjectionService _sut;

    public MemoryInjectionServiceTests()
    {
        _vectorStore = Substitute.For<IVectorStore>();
        _logger = Substitute.For<ILogger<MemoryInjectionService>>();
        _sut = new MemoryInjectionService(_vectorStore, _logger);
    }

    [Fact]
    public async Task VectorizeInsights_UpsertsDocuments_WhenInsightsExist()
    {
        // Arrange
        var insights = new SessionInsights
        {
            Facts = new List<string> { "User likes Python" },
            Decisions = new List<string> { "Use PostgreSQL" },
            Preferences = new List<string> { "Concise code" },
            ActionItems = new List<string> { "Fix bug X" }
        };

        // Act
        var result = await _sut.VectorizeInsightsAsync(insights, "u1", "t1", "session-001");

        // Assert
        result.DocumentsCreated.Should().Be(4);
        await _vectorStore.Received(4).UpsertAsync(Arg.Any<EmbeddingDocument>());
    }

    [Fact]
    public async Task VectorizeInsights_ReturnsZero_WhenInsightsEmpty()
    {
        // Arrange
        var insights = new SessionInsights();

        // Act
        var result = await _sut.VectorizeInsightsAsync(insights, "u1", "t1", "session-001");

        // Assert
        result.DocumentsCreated.Should().Be(0);
        await _vectorStore.DidNotReceive().UpsertAsync(Arg.Any<EmbeddingDocument>());
    }

    [Fact]
    public async Task BuildMemoryContext_ReturnsFormattedString_WhenMatchesFound()
    {
        // Arrange
        var matches = new List<SearchMatch>
        {
            new() { Content = "Prefers Python", Metadata = new Dictionary<string, string> { ["memoryType"] = "fact" } },
            new() { Content = "Postgres chosen", Metadata = new Dictionary<string, string> { ["memoryType"] = "decision" } }
        };
        _vectorStore.SearchWithFiltersAsync(Arg.Any<string>(), Arg.Any<Dictionary<string, string>>())
            .Returns(new SearchResult { Matches = matches });

        // Act
        var result = await _sut.BuildMemoryContextAsync("query", "u1", "t1");

        // Assert
        result.Should().Contain("RELEVANT CONTEXT");
        result.Should().Contain("Facts:");
        result.Should().Contain("Prefers Python");
        result.Should().Contain("Decisions:");
        result.Should().Contain("Postgres chosen");
    }

    [Fact]
    public async Task BuildMemoryContext_ReturnsEmpty_WhenNoMatches()
    {
        // Arrange
        _vectorStore.SearchWithFiltersAsync(Arg.Any<string>(), Arg.Any<Dictionary<string, string>>())
            .Returns(new SearchResult { Matches = new List<SearchMatch>() });

        // Act
        var result = await _sut.BuildMemoryContextAsync("query", "u1", "t1");

        // Assert
        result.Should().BeEmpty();
    }
}
