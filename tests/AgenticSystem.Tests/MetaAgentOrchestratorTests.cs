using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Core.Services;

namespace AgenticSystem.Tests;

public class MetaAgentOrchestratorTests
{
    private readonly IFrameworkOrchestratorService _frameworkOrchestrator;
    private readonly IDirectAgentRequestExecutor _directAgentRequestExecutor;
    private readonly ILLMRuntimeContextAccessor _llmRuntimeContextAccessor;
    private readonly IAgentFactory _agentFactory;
    private readonly ISessionManager _sessionManager;
    private readonly IAgentRuntimeCoordinator _runtimeCoordinator;
    private readonly ILogger<MetaAgentOrchestrator> _logger;
    private readonly MetaAgentOrchestrator _sut;

    public MetaAgentOrchestratorTests()
    {
        _frameworkOrchestrator = Substitute.For<IFrameworkOrchestratorService>();
        _directAgentRequestExecutor = Substitute.For<IDirectAgentRequestExecutor>();
        _llmRuntimeContextAccessor = Substitute.For<ILLMRuntimeContextAccessor>();
        _agentFactory = Substitute.For<IAgentFactory>();
        _sessionManager = Substitute.For<ISessionManager>();
        _runtimeCoordinator = Substitute.For<IAgentRuntimeCoordinator>();
        _logger = Substitute.For<ILogger<MetaAgentOrchestrator>>();

        _runtimeCoordinator.BeginExecutionScope(Arg.Any<string>(), Arg.Any<UserContext>())
            .Returns(Substitute.For<IDisposable>());
            
        _llmRuntimeContextAccessor.BeginScope(Arg.Any<UserContext>(), Arg.Any<string>())
            .Returns(Substitute.For<IDisposable>());

        _sut = new MetaAgentOrchestrator(_frameworkOrchestrator, _directAgentRequestExecutor, _llmRuntimeContextAccessor, _agentFactory, _sessionManager, _runtimeCoordinator, _logger);
    }

    [Fact]
    public async Task ProcessRequestAsync_WithValidInput_ReturnsSuccessResponse()
    {
        var input = "What time is it?";
        var userContext = new UserContext { UserId = "user1", Name = "Test" };
        var sessionId = "session-1";

        _sessionManager.StartSessionAsync(userContext).Returns(sessionId);
        _frameworkOrchestrator.ExecuteAsync(sessionId, input, userContext, Arg.Any<CancellationToken>())
            .Returns(AgentResponse.Ok("It's 10 AM", "GeneralAgent", AgentTier.Support));

        var result = await _sut.ProcessRequestAsync(input, userContext);

        result.Success.Should().BeTrue();
        result.Content.Should().Be("It's 10 AM");
        result.AgentName.Should().Be("GeneralAgent");
    }

    [Fact]
    public async Task ProcessRequestAsync_DelegatesToExecutionWorkflow()
    {
        var input = "some request";
        var userContext = new UserContext { UserId = "user1" };
        var sessionId = "session-1";

        _sessionManager.StartSessionAsync(userContext).Returns(sessionId);
        _frameworkOrchestrator.ExecuteAsync(sessionId, input, userContext, Arg.Any<CancellationToken>())
            .Returns(AgentResponse.Ok("response", "Agent", AgentTier.Support));

        await _sut.ProcessRequestAsync(input, userContext);

        await _frameworkOrchestrator.Received(1).ExecuteAsync(sessionId, input, userContext, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessRequestAsync_StartsSession_AndBeginsScope()
    {
        var input = "test";
        var userContext = new UserContext { UserId = "user1" };
        var sessionId = "session-1";

        _sessionManager.StartSessionAsync(userContext).Returns(sessionId);
        _frameworkOrchestrator.ExecuteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<UserContext>(), Arg.Any<CancellationToken>())
            .Returns(AgentResponse.Ok("ok", "Agent", AgentTier.Support));

        await _sut.ProcessRequestAsync(input, userContext);

        await _sessionManager.Received(1).StartSessionAsync(userContext);
        _runtimeCoordinator.Received(1).BeginExecutionScope(sessionId, userContext);
    }

    [Fact]
    public async Task CleanupInactiveAgentsAsync_ExecutesWithoutError()
    {
        _agentFactory.GetAgentsByTierAsync(Arg.Any<AgentTier>())
            .Returns(Enumerable.Empty<AgentInfo>());

        await _sut.Invoking(s => s.CleanupInactiveAgentsAsync())
            .Should().NotThrowAsync();
    }

    [Fact]
    public async Task GetActiveAgentsAsync_DelegatesToFactory()
    {
        var agents = new List<AgentInfo> { new() { Name = "Chief", Tier = AgentTier.Chief } };
        _agentFactory.GetAllAgentsAsync().Returns(agents);

        var result = await _sut.GetActiveAgentsAsync();

        result.Should().HaveCount(1);
        result.First().Name.Should().Be("Chief");
    }

    [Fact]
    public async Task ProcessDirectRequestAsync_DelegatesToDirectAgentRequestExecutor()
    {
        var input = "hello";
        var userContext = new UserContext { UserId = "user-1" };
        var sessionId = "session-1";
        var targetAgent = "FinanceAgent";

        _sessionManager.StartSessionAsync(userContext).Returns(sessionId);
        _directAgentRequestExecutor.ExecuteAsync(sessionId, input, userContext, targetAgent, Arg.Any<CancellationToken>())
            .Returns(AgentResponse.Ok("Direct response", targetAgent, AgentTier.Specialist));

        var result = await _sut.ProcessDirectRequestAsync(input, userContext, targetAgent);

        result.Success.Should().BeTrue();
        await _directAgentRequestExecutor.Received(1).ExecuteAsync(sessionId, input, userContext, targetAgent, Arg.Any<CancellationToken>());
    }
}
