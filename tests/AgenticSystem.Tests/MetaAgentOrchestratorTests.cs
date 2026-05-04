using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Core.Services;

namespace AgenticSystem.Tests;

public class MetaAgentOrchestratorTests
{
    private readonly IContextAnalyzer _contextAnalyzer;
    private readonly IAgentFactory _agentFactory;
    private readonly ISessionManager _sessionManager;
    private readonly IDynamicAgentService _dynamicAgentService;
    private readonly IHandoffManager _handoffManager;
    private readonly IToolAvailabilityGuard _toolGuard;
    private readonly IConfidenceScoreCalculator _confidenceCalculator;
    private readonly ILogger<MetaAgentOrchestrator> _logger;
    private readonly MetaAgentOrchestrator _sut;

    public MetaAgentOrchestratorTests()
    {
        _contextAnalyzer = Substitute.For<IContextAnalyzer>();
        _agentFactory = Substitute.For<IAgentFactory>();
        _sessionManager = Substitute.For<ISessionManager>();
        _dynamicAgentService = Substitute.For<IDynamicAgentService>();
        _handoffManager = Substitute.For<IHandoffManager>();
        _toolGuard = Substitute.For<IToolAvailabilityGuard>();
        _confidenceCalculator = Substitute.For<IConfidenceScoreCalculator>();
        _logger = Substitute.For<ILogger<MetaAgentOrchestrator>>();

        // Default: all tools available
        _toolGuard.CheckAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(ToolAvailabilityResult.FullCoverage(Array.Empty<string>()));
        _confidenceCalculator.Calculate(Arg.Any<AgentResponse>(), Arg.Any<RAGContext?>(), Arg.Any<IEnumerable<Reflection>?>(), Arg.Any<ToolAvailabilityResult?>())
            .Returns(new ConfidenceScore { Value = 0.8, Level = ConfidenceLevel.High, Label = "✅ Alta confiança" });

        _sut = new MetaAgentOrchestrator(_contextAnalyzer, _agentFactory, _sessionManager, _dynamicAgentService, _handoffManager, _toolGuard, _confidenceCalculator, _logger);
    }

    [Fact]
    public async Task ProcessRequestAsync_WithValidInput_ReturnsSuccessResponse()
    {
        // Arrange
        var input = "What time is it?";
        var userContext = new UserContext { UserId = "user1", Name = "Test" };
        var sessionId = "session-1";
        var analysis = new AnalysisResult
        {
            Intent = IntentType.Chat,
            PrimaryDomain = "general",
            Complexity = ComplexityLevel.Simple,
            RecommendedTier = AgentTier.Support,
            Confidence = 0.9
        };
        var agent = Substitute.For<IAgent>();
        agent.Name.Returns("GeneralAgent");
        agent.Tier.Returns(AgentTier.Support);
        agent.ExecuteAsync(input, userContext).Returns(AgentResponse.Ok("It's 10 AM", "GeneralAgent", AgentTier.Support));

        _sessionManager.StartSessionAsync(userContext).Returns(sessionId);
        _contextAnalyzer.AnalyzeAsync(input, userContext).Returns(analysis);
        _agentFactory.GetOrCreateAgentAsync(analysis).Returns(agent);
        _dynamicAgentService.IsAgentCreationRequestAsync(input, analysis).Returns(false);
        _handoffManager.EvaluateHandoffAsync(analysis, agent).Returns(new HandoffDecision { ShouldHandoff = false });

        // Act
        var result = await _sut.ProcessRequestAsync(input, userContext);

        // Assert
        result.Success.Should().BeTrue();
        result.Content.Should().Be("It's 10 AM");
        result.AgentName.Should().Be("GeneralAgent");
        result.SessionId.Should().Be(sessionId);
    }

    [Fact]
    public async Task ProcessRequestAsync_WithEmptyInput_ReturnsError()
    {
        // Arrange
        var userContext = new UserContext { UserId = "user1" };
        var analysis = new AnalysisResult { Confidence = 0.9 };

        _sessionManager.StartSessionAsync(userContext).Returns("session-1");
        _contextAnalyzer.AnalyzeAsync("", userContext).Returns(analysis);

        // Act
        var result = await _sut.ProcessRequestAsync("", userContext);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("vazio");
    }

    [Fact]
    public async Task ProcessRequestAsync_WithLowConfidence_ReturnsError()
    {
        // Arrange
        var input = "asdfgh";
        var userContext = new UserContext { UserId = "user1" };
        var analysis = new AnalysisResult { Confidence = 0.1 };

        _sessionManager.StartSessionAsync(userContext).Returns("session-1");
        _contextAnalyzer.AnalyzeAsync(input, userContext).Returns(analysis);

        // Act
        var result = await _sut.ProcessRequestAsync(input, userContext);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("específico");
    }

    [Fact]
    public async Task CleanupInactiveAgentsAsync_ExecutesWithoutError()
    {
        // Arrange
        _agentFactory.GetAgentsByTierAsync(Arg.Any<AgentTier>())
            .Returns(Enumerable.Empty<AgentInfo>());

        // Act & Assert
        await _sut.Invoking(s => s.CleanupInactiveAgentsAsync())
            .Should().NotThrowAsync();
    }

    [Fact]
    public async Task GetActiveAgentsAsync_DelegatesToFactory()
    {
        // Arrange
        var agents = new List<AgentInfo> { new() { Name = "Chief", Tier = AgentTier.Chief } };
        _agentFactory.GetAllAgentsAsync().Returns(agents);

        // Act
        var result = await _sut.GetActiveAgentsAsync();

        // Assert
        result.Should().HaveCount(1);
        result.First().Name.Should().Be("Chief");
    }
}
