using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Core.Services;

namespace AgenticSystem.Tests;

public class SemanticCompressorServiceTests
{
    private readonly ISessionManager _sessionManager;
    private readonly SemanticCompressorService _sut;

    public SemanticCompressorServiceTests()
    {
        _sessionManager = Substitute.For<ISessionManager>();
        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(ISessionManager)).Returns(_sessionManager);
        var logger = Substitute.For<ILogger<SemanticCompressorService>>();
        _sut = new SemanticCompressorService(serviceProvider, logger);
    }

    [Fact]
    public async Task CompressSessionAsync_EmptySession_ReturnsEmptySummary()
    {
        _sessionManager.GetRecentEventsAsync("s1", 50)
            .Returns(new List<AgentEvent>());

        var summary = await _sut.CompressSessionAsync("s1");

        summary.SourceType.Should().Be("session");
        summary.CompressedKnowledge.Should().Contain("Empty session");
        summary.CompressionRatio.Should().Be(1.0);
    }

    [Fact]
    public async Task CompressSessionAsync_WithEvents_CompressesAndExtractsInsights()
    {
        var events = new List<AgentEvent>
        {
            new()
            {
                SessionId = "s1",
                AgentName = "Agent1",
                UserInput = "What is X?",
                AgentResponse = "X is a concept...",
                ToolsUsed = new List<string> { "search" }
            },
            new()
            {
                SessionId = "s1",
                AgentName = "Agent1",
                UserInput = "Tell me more",
                AgentResponse = "Here are more details...",
                ToolsUsed = new List<string>()
            },
            new()
            {
                SessionId = "s1",
                AgentName = "Agent2",
                UserInput = "Summarize",
                AgentResponse = "Summary...",
                ToolsUsed = new List<string> { "compress" }
            }
        };

        _sessionManager.GetRecentEventsAsync("s1", 50).Returns(events);

        var summary = await _sut.CompressSessionAsync("s1");

        summary.CompressedKnowledge.Should().Contain("Agent1");
        summary.CompressedKnowledge.Should().Contain("Agent2");
        summary.KeyPrinciples.Should().NotBeEmpty();
        summary.CompressionRatio.Should().BeGreaterThan(0);
        summary.OriginalTokenCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task CompressChunksAsync_ReturnsCompressedSummary()
    {
        var chunkIds = new List<string> { "c1", "c2", "c3" };

        var summary = await _sut.CompressChunksAsync(chunkIds, "test-group");

        summary.SourceType.Should().Be("chunks");
        summary.SourceIds.Should().HaveCount(3);
        summary.CompressedKnowledge.Should().Contain("test-group");
    }

    [Fact]
    public async Task GetInsightsAsync_ReturnsStoredSummaries()
    {
        _sessionManager.GetRecentEventsAsync(Arg.Any<string>(), Arg.Any<int>())
            .Returns(new List<AgentEvent>
            {
                new() { SessionId = "s1", AgentName = "A", UserInput = "q", AgentResponse = "a", ToolsUsed = new() }
            });

        await _sut.CompressSessionAsync("s1");
        await _sut.CompressChunksAsync(new[] { "c1" }, "label");

        var allInsights = await _sut.GetInsightsAsync();
        allInsights.Should().HaveCount(2);

        var sessionInsights = await _sut.GetInsightsAsync("session");
        sessionInsights.Should().HaveCount(1);
    }
}
