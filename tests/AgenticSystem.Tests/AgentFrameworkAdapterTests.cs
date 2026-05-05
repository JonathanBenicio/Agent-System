using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.AgentFramework;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Tests;

public class AgentFrameworkAdapterTests
{
    [Fact]
    public void Constructor_ThrowsOnNullInnerAgent()
    {
        var act = () => new AgentFrameworkAdapter(
            null!,
            Substitute.For<Microsoft.Agents.AI.AIAgent>(),
            new AgentSessionBridge(Substitute.For<ISessionManager>(), Substitute.For<ILogger<AgentSessionBridge>>()),
            Substitute.For<ILogger<AgentFrameworkAdapter>>());
        act.Should().Throw<ArgumentNullException>().WithParameterName("innerAgent");
    }

    [Fact]
    public void Constructor_ThrowsOnNullFrameworkAgent()
    {
        var act = () => new AgentFrameworkAdapter(
            Substitute.For<IAgent>(),
            null!,
            new AgentSessionBridge(Substitute.For<ISessionManager>(), Substitute.For<ILogger<AgentSessionBridge>>()),
            Substitute.For<ILogger<AgentFrameworkAdapter>>());
        act.Should().Throw<ArgumentNullException>().WithParameterName("frameworkAgent");
    }

    [Fact]
    public void Constructor_ThrowsOnNullSessionBridge()
    {
        var act = () => new AgentFrameworkAdapter(
            Substitute.For<IAgent>(),
            Substitute.For<Microsoft.Agents.AI.AIAgent>(),
            null!,
            Substitute.For<ILogger<AgentFrameworkAdapter>>());
        act.Should().Throw<ArgumentNullException>().WithParameterName("sessionBridge");
    }

    [Fact]
    public void Constructor_ThrowsOnNullLogger()
    {
        var act = () => new AgentFrameworkAdapter(
            Substitute.For<IAgent>(),
            Substitute.For<Microsoft.Agents.AI.AIAgent>(),
            new AgentSessionBridge(Substitute.For<ISessionManager>(), Substitute.For<ILogger<AgentSessionBridge>>()),
            null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    [Fact]
    public void Properties_DelegateToInnerAgent()
    {
        var inner = Substitute.For<IAgent>();
        inner.Name.Returns("TestAgent");
        inner.Description.Returns("Test desc");
        inner.Tier.Returns(AgentTier.Specialist);
        inner.Domain.Returns("test");
        inner.IsActive.Returns(true);
        inner.Instructions.Returns("system prompt");
        inner.AvailableTools.Returns(new[] { "tool1" });

        var sut = new AgentFrameworkAdapter(
            inner,
            Substitute.For<Microsoft.Agents.AI.AIAgent>(),
            new AgentSessionBridge(Substitute.For<ISessionManager>(), Substitute.For<ILogger<AgentSessionBridge>>()),
            Substitute.For<ILogger<AgentFrameworkAdapter>>());

        sut.Name.Should().Be("TestAgent");
        sut.Description.Should().Be("Test desc");
        sut.Tier.Should().Be(AgentTier.Specialist);
        sut.Domain.Should().Be("test");
        sut.IsActive.Should().BeTrue();
        sut.Instructions.Should().Be("system prompt");
        sut.AvailableTools.Should().Contain("tool1");
    }

    [Fact]
    public async Task CanHandleAsync_DelegatesToInnerAgent()
    {
        var inner = Substitute.For<IAgent>();
        inner.CanHandleAsync(Arg.Any<AnalysisResult>()).Returns(true);

        var sut = new AgentFrameworkAdapter(
            inner,
            Substitute.For<Microsoft.Agents.AI.AIAgent>(),
            new AgentSessionBridge(Substitute.For<ISessionManager>(), Substitute.For<ILogger<AgentSessionBridge>>()),
            Substitute.For<ILogger<AgentFrameworkAdapter>>());

        var result = await sut.CanHandleAsync(new AnalysisResult());
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_FallsBackToInnerAgent_WhenFrameworkThrows()
    {
        // Arrange — inner agent returns a known fallback response
        var inner = Substitute.For<IAgent>();
        var fallbackResponse = AgentResponse.Ok("fallback", "test", AgentTier.Specialist);
        inner.ExecuteAsync("hello", Arg.Any<UserContext>()).Returns(fallbackResponse);

        // The AIAgent substitute will naturally fail (RunAsync is non-virtual
        // and the proxy isn't properly initialized), triggering the adapter's
        // catch → fallback path.
        var frameworkAgent = Substitute.For<Microsoft.Agents.AI.AIAgent>();

        var sessionManager = Substitute.For<ISessionManager>();
        var sessionBridge = new AgentSessionBridge(
            sessionManager,
            Substitute.For<ILogger<AgentSessionBridge>>());

        var sut = new AgentFrameworkAdapter(inner, frameworkAgent, sessionBridge,
            Substitute.For<ILogger<AgentFrameworkAdapter>>());

        // Act
        var result = await sut.ExecuteAsync("hello", new UserContext { UserId = "u1" });

        // Assert — adapter fell back to inner agent
        result.Content.Should().Be("fallback");
        result.Metadata.Should().ContainKey("frameworkFallback");
        result.Metadata["frameworkFallback"].Should().Be(true);
        result.Metadata.Should().ContainKey("frameworkError");
    }

    [Fact]
    public void Unwrap_ReturnsInnerAgent_WhenAdapterProvided()
    {
        var inner = Substitute.For<IAgent>();
        var sut = new AgentFrameworkAdapter(
            inner,
            Substitute.For<Microsoft.Agents.AI.AIAgent>(),
            new AgentSessionBridge(Substitute.For<ISessionManager>(), Substitute.For<ILogger<AgentSessionBridge>>()),
            Substitute.For<ILogger<AgentFrameworkAdapter>>());

        AgentFrameworkAdapter.Unwrap(sut).Should().BeSameAs(inner);
    }

    [Fact]
    public void Unwrap_ReturnsOriginalAgent_WhenAdapterNotProvided()
    {
        var inner = Substitute.For<IAgent>();

        AgentFrameworkAdapter.Unwrap(inner).Should().BeSameAs(inner);
    }

    [Fact]
    public void UpdateLastUsed_DelegatesToInnerAgent()
    {
        var inner = Substitute.For<IAgent>();
        var sut = new AgentFrameworkAdapter(
            inner,
            Substitute.For<Microsoft.Agents.AI.AIAgent>(),
            new AgentSessionBridge(Substitute.For<ISessionManager>(), Substitute.For<ILogger<AgentSessionBridge>>()),
            Substitute.For<ILogger<AgentFrameworkAdapter>>());

        sut.UpdateLastUsed();
        inner.Received(1).UpdateLastUsed();
    }
}
