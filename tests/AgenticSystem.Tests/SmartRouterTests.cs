using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Core.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace AgenticSystem.Tests;

public class SmartRouterTests
{
    private readonly IUserPreferenceEngine _preferenceEngine;
    private readonly ITriageService _triageService;
    private readonly IEnumerable<IFastPathInterceptor> _fastPathInterceptors;
    private readonly IExternalQuotaSyncService _quotaSyncService;
    private readonly SmartRouter _sut;

    public SmartRouterTests()
    {
        _preferenceEngine = Substitute.For<IUserPreferenceEngine>();
        _triageService = Substitute.For<ITriageService>();
        _fastPathInterceptors = Enumerable.Empty<IFastPathInterceptor>();
        _quotaSyncService = Substitute.For<IExternalQuotaSyncService>();
        var logger = Substitute.For<ILogger<SmartRouter>>();
        _sut = new SmartRouter(_preferenceEngine, _triageService, _fastPathInterceptors, _quotaSyncService, logger);
    }

    [Fact]
    public async Task RouteAsync_WithUserPreference_ReturnsPreferredAgent()
    {
        var analysis = new AnalysisResult
        {
            PrimaryDomain = "code",
            EstimatedAgent = "WorkAgent",
            Confidence = 0.8
        };
        var context = new UserContext { UserId = "user1" };

        _preferenceEngine.RecommendAgentAsync("user1", analysis)
            .Returns("PersonalAgent");

        var decision = await _sut.RouteAsync(analysis, context);

        decision.PrimaryAgent.Should().Be("PersonalAgent");
        decision.UsedUserPreference.Should().BeTrue();
        decision.ConfidenceScore.Should().Be(0.9);
    }

    [Fact]
    public async Task RouteAsync_NoPreference_NoMetrics_FallsBackToAnalysis()
    {
        var analysis = new AnalysisResult
        {
            PrimaryDomain = "code",
            EstimatedAgent = "WorkAgent",
            Confidence = 0.7
        };
        var context = new UserContext { UserId = "user1" };

        _preferenceEngine.RecommendAgentAsync("user1", analysis)
            .Returns((string?)null);

        var decision = await _sut.RouteAsync(analysis, context);

        decision.PrimaryAgent.Should().Be("WorkAgent");
        decision.UsedUserPreference.Should().BeFalse();
        decision.RoutingReason.Should().Contain("Default");
    }

    [Fact]
    public async Task RouteAsync_WithPerformanceData_UsesPerformanceRouting()
    {
        var analysis = new AnalysisResult
        {
            PrimaryDomain = "code",
            EstimatedAgent = "WorkAgent",
            Confidence = 0.7
        };
        var context = new UserContext { UserId = "user1" };

        _preferenceEngine.RecommendAgentAsync("user1", analysis)
            .Returns((string?)null);

        // Record enough performance data
        for (int i = 0; i < 5; i++)
        {
            await _sut.RecordPerformanceAsync("AnalysisAgent", new AgentPerformanceMetric
            {
                Latency = TimeSpan.FromMilliseconds(200),
                Success = true,
                Domain = "code",
                UserSatisfaction = 0.9
            });
        }

        var decision = await _sut.RouteAsync(analysis, context);

        decision.PrimaryAgent.Should().Be("AnalysisAgent");
        decision.UsedUserPreference.Should().BeFalse();
        decision.RoutingReason.Should().Contain("Performance");
    }

    [Fact]
    public async Task RecordPerformanceAsync_StoresMetrics()
    {
        await _sut.RecordPerformanceAsync("TestAgent", new AgentPerformanceMetric
        {
            Latency = TimeSpan.FromMilliseconds(100),
            Success = true,
            Domain = "code"
        });

        var rankings = (await _sut.GetRankingsByDomainAsync("code")).ToList();
        rankings.Should().HaveCount(1);
        rankings[0].AgentName.Should().Be("TestAgent");
        rankings[0].SuccessRate.Should().Be(1.0);
    }

    [Fact]
    public async Task GetRankingsByDomainAsync_CalculatesCorrectScores()
    {
        await _sut.RecordPerformanceAsync("AgentA", new AgentPerformanceMetric
        {
            Latency = TimeSpan.FromMilliseconds(100),
            Success = true,
            Domain = "data",
            UserSatisfaction = 0.9
        });

        await _sut.RecordPerformanceAsync("AgentA", new AgentPerformanceMetric
        {
            Latency = TimeSpan.FromMilliseconds(200),
            Success = false,
            Domain = "data",
            UserSatisfaction = 0.3
        });

        var rankings = (await _sut.GetRankingsByDomainAsync("data")).ToList();
        rankings.Should().HaveCount(1);
        rankings[0].SuccessRate.Should().Be(0.5);
        rankings[0].TotalRequests.Should().Be(2);
    }

    [Fact]
    public async Task GetRankingsByDomainAsync_FiltersCorrectDomain()
    {
        await _sut.RecordPerformanceAsync("AgentA", new AgentPerformanceMetric
        {
            Latency = TimeSpan.FromMilliseconds(100),
            Success = true,
            Domain = "code"
        });

        await _sut.RecordPerformanceAsync("AgentB", new AgentPerformanceMetric
        {
            Latency = TimeSpan.FromMilliseconds(100),
            Success = true,
            Domain = "data"
        });

        var codeRankings = (await _sut.GetRankingsByDomainAsync("code")).ToList();
        codeRankings.Should().HaveCount(1);
        codeRankings[0].AgentName.Should().Be("AgentA");
    }

    [Fact]
    public async Task GetRankingsByDomainAsync_EmptyDomain_ReturnsEmpty()
    {
        var rankings = (await _sut.GetRankingsByDomainAsync("nonexistent")).ToList();
        rankings.Should().BeEmpty();
    }

    [Fact]
    public async Task RouteAsync_PerformanceThreshold_RequiresMinRequests()
    {
        var analysis = new AnalysisResult
        {
            PrimaryDomain = "code",
            EstimatedAgent = "WorkAgent",
            Confidence = 0.7
        };
        var context = new UserContext { UserId = "user1" };

        _preferenceEngine.RecommendAgentAsync("user1", analysis)
            .Returns((string?)null);

        // Only 2 requests — below threshold of 3
        for (int i = 0; i < 2; i++)
        {
            await _sut.RecordPerformanceAsync("AnalysisAgent", new AgentPerformanceMetric
            {
                Latency = TimeSpan.FromMilliseconds(100),
                Success = true,
                Domain = "code"
            });
        }

        var decision = await _sut.RouteAsync(analysis, context);

        // Should fall back to analysis-based routing (not enough data)
        decision.PrimaryAgent.Should().Be("WorkAgent");
    }
}
