using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.LLM.Interfaces;
using AgenticSystem.Core.LLM.Models;
using AgenticSystem.Core.Models;
using AgenticSystem.Core.Services;
using System.Threading;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace AgenticSystem.Tests;

public class SessionConsolidatorTests
{
    private readonly ILLMManager _llmManager;
    private readonly SessionConsolidator _sut;

    public SessionConsolidatorTests()
    {
        _llmManager = Substitute.For<ILLMManager>();
        var logger = Substitute.For<ILogger<SessionConsolidator>>();
        _sut = new SessionConsolidator(_llmManager, logger);
    }

    [Fact]
    public async Task SummarizeSessionAsync_WithEvents_ReturnsSummary()
    {
        var events = new List<AgentEvent>
        {
            new()
            {
                SessionId = "s1",
                AgentName = "WorkAgent",
                AgentTier = AgentTier.Master,
                UserInput = "Help me with code",
                AgentResponse = "Here is the code...",
                Timestamp = DateTime.UtcNow.AddMinutes(-10)
            },
            new()
            {
                SessionId = "s1",
                AgentName = "WorkAgent",
                AgentTier = AgentTier.Master,
                UserInput = "Fix the bug",
                AgentResponse = "Bug fixed!",
                Timestamp = DateTime.UtcNow
            }
        };

        var llmJson = @"{
            ""summary"": ""User requested code help and bug fix"",
            ""topicsDiscussed"": [""coding"", ""debugging""],
            ""agentsUsed"": [""WorkAgent""]
        }";
        _llmManager.GenerateAsync(Arg.Any<LLMRequest>(), Arg.Any<CancellationToken>())
            .Returns(new LLMResponse { Success = true, Content = llmJson });

        var result = await _sut.SummarizeSessionAsync("s1", events);

        result.SessionId.Should().Be("s1");
        result.Summary.Should().NotBeNullOrWhiteSpace();
        result.EventCount.Should().Be(2);
        result.AgentsUsed.Should().Contain("WorkAgent");
    }

    [Fact]
    public async Task SummarizeSessionAsync_EmptyEvents_ReturnsFallback()
    {
        var result = await _sut.SummarizeSessionAsync("s1", []);

        result.SessionId.Should().Be("s1");
        result.EventCount.Should().Be(0);
    }

    [Fact]
    public async Task SummarizeSessionAsync_LLMFails_ReturnsFallback()
    {
        var events = new List<AgentEvent>
        {
            new()
            {
                SessionId = "s1",
                AgentName = "WorkAgent",
                AgentTier = AgentTier.Master,
                UserInput = "Help",
                AgentResponse = "Done",
                Timestamp = DateTime.UtcNow
            }
        };

        _llmManager.GenerateAsync(Arg.Any<LLMRequest>(), Arg.Any<CancellationToken>())
            .Returns(new LLMResponse { Success = false });

        var result = await _sut.SummarizeSessionAsync("s1", events);

        result.SessionId.Should().Be("s1");
        result.Summary.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ExtractInsightsAsync_WithEvents_ReturnsInsights()
    {
        var events = new List<AgentEvent>
        {
            new()
            {
                SessionId = "s1",
                AgentName = "WorkAgent",
                AgentTier = AgentTier.Master,
                UserInput = "deploy to prod",
                AgentResponse = "Deployed successfully",
                Timestamp = DateTime.UtcNow
            }
        };

        var llmJson = @"{
            ""facts"": [""User deployed to production""],
            ""decisions"": [""Use blue-green deployment""],
            ""preferences"": [""Prefers CLI over UI""],
            ""actionItems"": [""Set up monitoring""]
        }";
        _llmManager.GenerateAsync(Arg.Any<LLMRequest>(), Arg.Any<CancellationToken>())
            .Returns(new LLMResponse { Success = true, Content = llmJson });

        var result = await _sut.ExtractInsightsAsync("s1", events);

        result.SessionId.Should().Be("s1");
    }

    [Fact]
    public async Task ExtractInsightsAsync_LLMFails_ReturnsFallback()
    {
        var events = new List<AgentEvent>
        {
            new()
            {
                SessionId = "s1",
                AgentName = "WorkAgent",
                AgentTier = AgentTier.Master,
                UserInput = "test",
                AgentResponse = "result",
                Timestamp = DateTime.UtcNow
            }
        };

        _llmManager.GenerateAsync(Arg.Any<LLMRequest>(), Arg.Any<CancellationToken>())
            .Returns(new LLMResponse { Success = false });

        var result = await _sut.ExtractInsightsAsync("s1", events);

        result.SessionId.Should().Be("s1");
    }

    [Fact]
    public async Task GetRelevantSummariesAsync_AfterSummarize_FindsByKeyword()
    {
        var events = new List<AgentEvent>
        {
            new()
            {
                SessionId = "s1",
                AgentName = "WorkAgent",
                AgentTier = AgentTier.Master,
                UserInput = "Help with Kubernetes deployment",
                AgentResponse = "Done",
                Timestamp = DateTime.UtcNow
            }
        };

        var llmJson = @"{
            ""summary"": ""User needed help with Kubernetes deployment to production"",
            ""topics"": [""kubernetes"", ""deployment""]
        }";
        _llmManager.GenerateAsync(Arg.Any<LLMRequest>(), Arg.Any<CancellationToken>())
            .Returns(new LLMResponse { Success = true, Content = llmJson });

        await _sut.SummarizeSessionAsync("s1", events);
        var results = await _sut.GetRelevantSummariesAsync("kubernetes", 5);

        results.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetRelevantSummariesAsync_NoMatch_ReturnsEmpty()
    {
        var results = await _sut.GetRelevantSummariesAsync("nonexistent", 5);
        results.Should().BeEmpty();
    }
}
