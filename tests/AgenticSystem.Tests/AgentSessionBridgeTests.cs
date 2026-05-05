using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.AgentFramework;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace AgenticSystem.Tests;

public class AgentSessionBridgeTests
{
    private readonly ISessionManager _sessionManager;
    private readonly ILogger<AgentSessionBridge> _logger;
    private readonly AgentSessionBridge _sut;

    public AgentSessionBridgeTests()
    {
        _sessionManager = Substitute.For<ISessionManager>();
        _logger = Substitute.For<ILogger<AgentSessionBridge>>();
        _sut = new AgentSessionBridge(_sessionManager, _logger);
    }

    [Fact]
    public void Constructor_ThrowsOnNullSessionManager()
    {
        var act = () => new AgentSessionBridge(null!, _logger);
        act.Should().Throw<ArgumentNullException>().WithParameterName("sessionManager");
    }

    [Fact]
    public void Constructor_ThrowsOnNullLogger()
    {
        var act = () => new AgentSessionBridge(_sessionManager, null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    [Fact]
    public void ActiveSessionCount_InitiallyZero()
    {
        _sut.ActiveSessionCount.Should().Be(0);
    }

    [Fact]
    public void RemoveSession_NonExistentKey_DoesNotThrow()
    {
        var act = () => _sut.RemoveSession("nonexistent");
        act.Should().NotThrow();
    }

    [Fact]
    public async Task SyncResponseAsync_CreatesEventWithCorrectFields()
    {
        var response = new AgentResponse
        {
            Content = "test response",
            Success = true,
            ActionsPerformed = new List<string> { "action1" },
            ToolsUsed = new List<string> { "tool1" }
        };

        await _sut.SyncResponseAsync("session-1", "TestAgent", "hello", response);

        await _sessionManager.Received(1).AddEventAsync("session-1", Arg.Is<AgentEvent>(e =>
            e.SessionId == "session-1" &&
            e.AgentName == "TestAgent" &&
            e.UserInput == "hello" &&
            e.AgentResponse == "test response" &&
            e.Context.ContainsKey("source") &&
            (string)e.Context["source"] == "AgentFramework"));
    }

    [Fact]
    public async Task GetOrCreateFrameworkSessionAsync_WhenLatestStateBelongsToDifferentAgent_RestoresMatchingAgentState()
    {
        var agent = Substitute.For<Microsoft.Agents.AI.AIAgent>();
        var restoredSession = Substitute.For<Microsoft.Agents.AI.AgentSession>();
        agent.Id.Returns("agent-1");
        agent.DeserializeSessionAsync(Arg.Any<JsonElement>(), cancellationToken: Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(restoredSession));

        _sessionManager.GetRecentEventsAsync("session-1", 200).Returns(
        [
            new AgentEvent
            {
                SessionId = "session-1",
                Timestamp = DateTime.UtcNow,
                Context = new Dictionary<string, object>
                {
                    ["frameworkAgentId"] = "agent-2",
                    ["frameworkSessionState"] = "{not-json"
                }
            },
            new AgentEvent
            {
                SessionId = "session-1",
                Timestamp = DateTime.UtcNow.AddMinutes(-1),
                Context = new Dictionary<string, object>
                {
                    ["frameworkAgentId"] = "agent-1",
                    ["frameworkSessionState"] = "{}"
                }
            }
        ]);

        var result = await _sut.GetOrCreateFrameworkSessionAsync(agent, "session-1");

        result.Should().BeSameAs(restoredSession);
        await agent.Received(1).DeserializeSessionAsync(Arg.Any<JsonElement>(), cancellationToken: Arg.Any<CancellationToken>());
        await agent.DidNotReceive().CreateSessionAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetOrCreateFrameworkSessionAsync_WhenOnlyDifferentAgentStateExists_CreatesNewSession()
    {
        var agent = Substitute.For<Microsoft.Agents.AI.AIAgent>();
        var newSession = Substitute.For<Microsoft.Agents.AI.AgentSession>();
        agent.Id.Returns("agent-1");
        agent.CreateSessionAsync(Arg.Any<CancellationToken>()).Returns(ValueTask.FromResult(newSession));

        _sessionManager.GetRecentEventsAsync("session-1", 200).Returns(
        [
            new AgentEvent
            {
                SessionId = "session-1",
                Timestamp = DateTime.UtcNow,
                Context = new Dictionary<string, object>
                {
                    ["frameworkAgentId"] = "agent-2",
                    ["frameworkSessionState"] = "{}"
                }
            }
        ]);

        var result = await _sut.GetOrCreateFrameworkSessionAsync(agent, "session-1");

        result.Should().BeSameAs(newSession);
        await agent.Received(1).CreateSessionAsync(Arg.Any<CancellationToken>());
    }
}
