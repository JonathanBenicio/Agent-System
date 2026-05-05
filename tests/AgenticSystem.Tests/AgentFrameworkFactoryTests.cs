using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.AgentFramework;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Tests;

public class AgentFrameworkFactoryTests
{
    private readonly IChatClient _chatClient;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IServiceProvider _serviceProvider;
    private readonly AgentFrameworkFactory _sut;

    public AgentFrameworkFactoryTests()
    {
        _chatClient = Substitute.For<IChatClient>();
        _loggerFactory = Substitute.For<ILoggerFactory>();
        _loggerFactory.CreateLogger(Arg.Any<string>()).Returns(Substitute.For<ILogger>());
        _serviceProvider = Substitute.For<IServiceProvider>();
        _sut = new AgentFrameworkFactory(_chatClient, _loggerFactory, _serviceProvider);
    }

    [Fact]
    public void CreateFromAgent_ThrowsOnNull()
    {
        var act = () => _sut.CreateFromAgent(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void CreateFromAgent_ReturnsFrameworkAgent()
    {
        var agent = Substitute.For<IAgent>();
        agent.Name.Returns("test-agent");
        agent.Description.Returns("A test agent");
        agent.Instructions.Returns("You are a test agent.");

        var result = _sut.CreateFromAgent(agent);

        result.Should().NotBeNull();
        result.Name.Should().Be("test-agent");
        result.Description.Should().Be("A test agent");
    }

    [Fact]
    public void CreateFromSpecification_ThrowsOnNull()
    {
        var act = () => _sut.CreateFromSpecification(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void CreateFromSpecification_ReturnsFrameworkAgent()
    {
        var spec = new AgentSpecification
        {
            Name = "dynamic-agent",
            Description = "Dynamic agent",
            Instructions = "You help with things."
        };

        var result = _sut.CreateFromSpecification(spec);

        result.Should().NotBeNull();
        result.Name.Should().Be("dynamic-agent");
        result.Description.Should().Be("Dynamic agent");
    }
}
