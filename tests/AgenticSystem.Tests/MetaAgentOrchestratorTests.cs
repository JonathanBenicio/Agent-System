using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Core.Services;

namespace AgenticSystem.Tests;

public class MetaAgentOrchestratorTests
{
    private readonly IAgentExecutionWorkflow _executionWorkflow;
    private readonly IAgentFactory _agentFactory;
    private readonly ISessionManager _sessionManager;
    private readonly IAgentRuntimeCoordinator _runtimeCoordinator;
    private readonly ILogger<MetaAgentOrchestrator> _logger;
    private readonly MetaAgentOrchestrator _sut;

    public MetaAgentOrchestratorTests()
    {
        _executionWorkflow = Substitute.For<IAgentExecutionWorkflow>();
        _agentFactory = Substitute.For<IAgentFactory>();
        _sessionManager = Substitute.For<ISessionManager>();
        _runtimeCoordinator = Substitute.For<IAgentRuntimeCoordinator>();
        _logger = Substitute.For<ILogger<MetaAgentOrchestrator>>();

        _runtimeCoordinator.BeginExecutionScope(Arg.Any<string>(), Arg.Any<UserContext>())
            .Returns(Substitute.For<IDisposable>());

        _sut = new MetaAgentOrchestrator(_executionWorkflow, _agentFactory, _sessionManager, _runtimeCoordinator, _logger);
    }

    [Fact]
    public async Task ProcessRequestAsync_WithValidInput_ReturnsSuccessResponse()
    {
        // Arrange
        var input = "What time is it?";
        var userContext = new UserContext { UserId = "user1", Name = "Test" };
        var sessionId = "session-1";

        _sessionManager.StartSessionAsync(userContext).Returns(sessionId);
        _executionWorkflow.ExecuteAsync(sessionId, input, userContext, Arg.Any<CancellationToken>())
            .Returns(AgentResponse.Ok("It's 10 AM", "GeneralAgent", AgentTier.Support));

        // Act
        var result = await _sut.ProcessRequestAsync(input, userContext);

        // Assert
        result.Success.Should().BeTrue();
        result.Content.Should().Be("It's 10 AM");
        result.AgentName.Should().Be("GeneralAgent");
    }

    [Fact]
    public async Task ProcessRequestAsync_DelegatesToExecutionWorkflow()
    {
        // Arrange
        var input = "some request";
        var userContext = new UserContext { UserId = "user1" };
        var sessionId = "session-1";

        _sessionManager.StartSessionAsync(userContext).Returns(sessionId);
        _executionWorkflow.ExecuteAsync(sessionId, input, userContext, Arg.Any<CancellationToken>())
            .Returns(AgentResponse.Ok("response", "Agent", AgentTier.Support));

        // Act
        await _sut.ProcessRequestAsync(input, userContext);

        // Assert
        await _executionWorkflow.Received(1).ExecuteAsync(sessionId, input, userContext, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessRequestAsync_StartsSession_AndBeginsScope()
    {
        // Arrange
        var input = "test";
        var userContext = new UserContext { UserId = "user1" };
        var sessionId = "session-1";

        _sessionManager.StartSessionAsync(userContext).Returns(sessionId);
        _executionWorkflow.ExecuteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<UserContext>(), Arg.Any<CancellationToken>())
            .Returns(AgentResponse.Ok("ok", "Agent", AgentTier.Support));

        // Act
        await _sut.ProcessRequestAsync(input, userContext);

        // Assert
        await _sessionManager.Received(1).StartSessionAsync(userContext);
        _runtimeCoordinator.Received(1).BeginExecutionScope(sessionId, userContext);
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
