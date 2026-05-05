using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.AgentFramework;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Tests;

public class AgentFrameworkAgentFactoryTests
{
    private readonly IAgentFactory _innerFactory;
    private readonly AgentFrameworkFactory _frameworkFactory;
    private readonly AgentSessionBridge _sessionBridge;
    private readonly ILogger<AgentFrameworkAdapter> _adapterLogger;

    public AgentFrameworkAgentFactoryTests()
    {
        _innerFactory = Substitute.For<IAgentFactory>();
        _frameworkFactory = Substitute.For<AgentFrameworkFactory>(
            Substitute.For<Microsoft.Extensions.AI.IChatClient>(),
            Substitute.For<ILoggerFactory>(),
            Substitute.For<IServiceProvider>(),
            (AgenticSystem.Infrastructure.MCP.McpToolsAIFunctionAdapter?)null);
        _sessionBridge = new AgentSessionBridge(
            Substitute.For<ISessionManager>(),
            Substitute.For<ILogger<AgentSessionBridge>>());
        _adapterLogger = Substitute.For<ILogger<AgentFrameworkAdapter>>();
    }

    [Fact]
    public void Constructor_ThrowsOnNullInner()
    {
        var act = () => new AgentFrameworkAgentFactory(null!, _frameworkFactory, _sessionBridge, _adapterLogger);
        act.Should().Throw<ArgumentNullException>().WithParameterName("inner");
    }

    [Fact]
    public void Constructor_ThrowsOnNullFrameworkFactory()
    {
        var act = () => new AgentFrameworkAgentFactory(_innerFactory, null!, _sessionBridge, _adapterLogger);
        act.Should().Throw<ArgumentNullException>().WithParameterName("frameworkFactory");
    }

    [Fact]
    public void Constructor_ThrowsOnNullSessionBridge()
    {
        var act = () => new AgentFrameworkAgentFactory(_innerFactory, _frameworkFactory, null!, _adapterLogger);
        act.Should().Throw<ArgumentNullException>().WithParameterName("sessionBridge");
    }

    [Fact]
    public void Constructor_ThrowsOnNullLogger()
    {
        var act = () => new AgentFrameworkAgentFactory(_innerFactory, _frameworkFactory, _sessionBridge, null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("adapterLogger");
    }

    [Fact]
    public void DetermineTier_DelegatesToInner()
    {
        _innerFactory.DetermineTier(ComplexityLevel.Complex).Returns(AgentTier.Master);
        var sut = new AgentFrameworkAgentFactory(_innerFactory, _frameworkFactory, _sessionBridge, _adapterLogger);

        sut.DetermineTier(ComplexityLevel.Complex).Should().Be(AgentTier.Master);
    }

    [Fact]
    public async Task GetAgentsByTierAsync_DelegatesToInner()
    {
        var agents = new List<AgentInfo> { new() { Name = "test" } };
        _innerFactory.GetAgentsByTierAsync(AgentTier.Specialist).Returns(agents);
        var sut = new AgentFrameworkAgentFactory(_innerFactory, _frameworkFactory, _sessionBridge, _adapterLogger);

        var result = await sut.GetAgentsByTierAsync(AgentTier.Specialist);
        result.Should().BeEquivalentTo(agents);
    }

    [Fact]
    public async Task GetAllAgentsAsync_DelegatesToInner()
    {
        var agents = new List<AgentInfo> { new() { Name = "a1" }, new() { Name = "a2" } };
        _innerFactory.GetAllAgentsAsync().Returns(agents);
        var sut = new AgentFrameworkAgentFactory(_innerFactory, _frameworkFactory, _sessionBridge, _adapterLogger);

        var result = await sut.GetAllAgentsAsync();
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task RemoveAgentAsync_DelegatesToInner()
    {
        _innerFactory.RemoveAgentAsync("test").Returns(true);
        var sut = new AgentFrameworkAgentFactory(_innerFactory, _frameworkFactory, _sessionBridge, _adapterLogger);

        (await sut.RemoveAgentAsync("test")).Should().BeTrue();
    }
}
