using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.AgentFramework;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Tests;

public class AgentFrameworkAgentFactoryTests
{
    private readonly AgentFrameworkFactory _frameworkFactory;
    private readonly ISessionManager _sessionManager;
    private readonly AgentFrameworkSessionStoreAdapter _sessionStore;
    private readonly ILogger<AgentFrameworkAdapter> _adapterLogger;

    public AgentFrameworkAgentFactoryTests()
    {
        var chatClient = Substitute.For<Microsoft.Extensions.AI.IChatClient>();
        var loggerFactory = Substitute.For<ILoggerFactory>();
        loggerFactory.CreateLogger(Arg.Any<string>()).Returns(Substitute.For<ILogger>());
        var serviceProvider = Substitute.For<IServiceProvider>();
        _frameworkFactory = new AgentFrameworkFactory(chatClient, loggerFactory, serviceProvider);
        _sessionManager = Substitute.For<ISessionManager>();
        _sessionStore = new AgentFrameworkSessionStoreAdapter(
            _sessionManager,
            Substitute.For<ILogger<AgentFrameworkSessionStoreAdapter>>());
        _adapterLogger = Substitute.For<ILogger<AgentFrameworkAdapter>>();
    }

    [Fact]
    public void Constructor_ThrowsOnNullFrameworkFactory()
    {
        var act = () => new AgentFrameworkAgentFactory(null!, _sessionStore, _sessionManager, _adapterLogger);
        act.Should().Throw<ArgumentNullException>().WithParameterName("frameworkFactory");
    }

    [Fact]
    public void Constructor_ThrowsOnNullSessionStore()
    {
        var act = () => new AgentFrameworkAgentFactory(_frameworkFactory, null!, _sessionManager, _adapterLogger);
        act.Should().Throw<ArgumentNullException>().WithParameterName("sessionStore");
    }

    [Fact]
    public void Constructor_ThrowsOnNullSessionManager()
    {
        var act = () => new AgentFrameworkAgentFactory(_frameworkFactory, _sessionStore, null!, _adapterLogger);
        act.Should().Throw<ArgumentNullException>().WithParameterName("sessionManager");
    }

    [Fact]
    public void Constructor_ThrowsOnNullLogger()
    {
        var act = () => new AgentFrameworkAgentFactory(_frameworkFactory, _sessionStore, _sessionManager, null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("adapterLogger");
    }

    [Fact]
    public async Task CreateDirectExecutionAgentAsync_WrapsRawAgent()
    {
        var agent = Substitute.For<IAgent>();
        agent.Name.Returns("test-agent");
        agent.Description.Returns("A test agent");
        agent.Instructions.Returns("You are a test agent.");
        agent.Domain.Returns("test");
        agent.Tier.Returns(AgentTier.Specialist);

        var sut = new AgentFrameworkAgentFactory(_frameworkFactory, _sessionStore, _sessionManager, _adapterLogger);
        var result = await sut.CreateDirectExecutionAgentAsync(agent);

        result.Should().BeOfType<AgentFrameworkAdapter>();
        result.Name.Should().Be("test-agent");
    }

    [Fact]
    public async Task CreateDirectExecutionAgentAsync_ReturnsSameInstance_WhenAlreadyWrapped()
    {
        var inner = Substitute.For<IAgent>();
        inner.Name.Returns("test-agent");
        inner.Description.Returns("A test agent");
        inner.Instructions.Returns("You are a test agent.");
        inner.Domain.Returns("test");
        inner.Tier.Returns(AgentTier.Specialist);

        var frameworkAgent = _frameworkFactory.CreateFromAgent(inner);
        var wrapped = new AgentFrameworkAdapter(inner, frameworkAgent, _sessionStore, _sessionManager, _adapterLogger);
        var sut = new AgentFrameworkAgentFactory(_frameworkFactory, _sessionStore, _sessionManager, _adapterLogger);

        var result = await sut.CreateDirectExecutionAgentAsync(wrapped);
        result.Should().BeSameAs(wrapped);
    }
}
