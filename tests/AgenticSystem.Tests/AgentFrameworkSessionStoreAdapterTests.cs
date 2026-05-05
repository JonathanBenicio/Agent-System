using System.Text.Json;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.AgentFramework;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Tests;

public class AgentFrameworkSessionStoreAdapterTests
{
    private readonly ISessionManager _sessionManager;
    private readonly ISessionStore _sessionStore;
    private readonly ILogger<AgentFrameworkSessionStoreAdapter> _logger;

    public AgentFrameworkSessionStoreAdapterTests()
    {
        _sessionManager = Substitute.For<ISessionManager>();
        _sessionStore = Substitute.For<ISessionStore>();
        _logger = Substitute.For<ILogger<AgentFrameworkSessionStoreAdapter>>();
    }

    [Fact]
    public async Task SaveSessionAsync_WhenSessionExists_PersistsUsingStableAgentNameKey()
    {
        var sessionData = new SessionData { Id = "session-1" };
        _sessionStore.GetAsync("session-1", Arg.Any<CancellationToken>()).Returns(sessionData);

        var agent = Substitute.For<AIAgent>();
        var frameworkSession = Substitute.For<AgentSession>();
        agent.Name.Returns("Orchestrator");
        agent.Id.Returns("volatile-agent-id");
        agent.SerializeSessionAsync(
                frameworkSession,
                Arg.Any<JsonSerializerOptions>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(ParseJson("{\"messages\":[]}")));

        var sut = new AgentFrameworkSessionStoreAdapter(_sessionManager, _logger, _sessionStore);

        await sut.SaveSessionAsync(agent, "session-1", frameworkSession, CancellationToken.None);

        sessionData.RuntimeSettings.Should().ContainKey("frameworkSessionState:orchestrator");
        sessionData.RuntimeSettings["frameworkSessionState:orchestrator"].Should().Be("{\"messages\":[]}");
        await _sessionStore.Received(1).SaveAsync(sessionData, Arg.Any<CancellationToken>());
        await _sessionManager.DidNotReceive().AddEventAsync(Arg.Any<string>(), Arg.Any<AgentEvent>());
    }

    [Fact]
    public async Task GetSessionAsync_WhenStableNameKeyExists_RestoresPersistedSession()
    {
        var restoredSession = Substitute.For<AgentSession>();
        _sessionStore.GetAsync("session-1", Arg.Any<CancellationToken>()).Returns(
            new SessionData
            {
                Id = "session-1",
                RuntimeSettings = new Dictionary<string, string>
                {
                    ["frameworkSessionState:orchestrator"] = "{}"
                }
            });

        var agent = Substitute.For<AIAgent>();
        agent.Name.Returns("Orchestrator");
        agent.Id.Returns("volatile-agent-id");
        agent.DeserializeSessionAsync(
                Arg.Any<JsonElement>(),
                Arg.Any<JsonSerializerOptions>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(restoredSession));

        var sut = new AgentFrameworkSessionStoreAdapter(_sessionManager, _logger, _sessionStore);

        var result = await sut.GetSessionAsync(agent, "session-1", CancellationToken.None);

        result.Should().BeSameAs(restoredSession);
        await agent.Received(1).DeserializeSessionAsync(
            Arg.Any<JsonElement>(),
            Arg.Any<JsonSerializerOptions>(),
            Arg.Any<CancellationToken>());
        await agent.DidNotReceive().CreateSessionAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetSessionAsync_WhenOnlyLegacyAgentIdKeyExists_RestoresPersistedSession()
    {
        var restoredSession = Substitute.For<AgentSession>();
        _sessionStore.GetAsync("session-1", Arg.Any<CancellationToken>()).Returns(
            new SessionData
            {
                Id = "session-1",
                RuntimeSettings = new Dictionary<string, string>
                {
                    ["frameworkSessionState:agent-legacy"] = "{}"
                }
            });

        var agent = Substitute.For<AIAgent>();
        agent.Name.Returns("Orchestrator");
        agent.Id.Returns("agent-legacy");
        agent.DeserializeSessionAsync(
                Arg.Any<JsonElement>(),
                Arg.Any<JsonSerializerOptions>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(restoredSession));

        var sut = new AgentFrameworkSessionStoreAdapter(_sessionManager, _logger, _sessionStore);

        var result = await sut.GetSessionAsync(agent, "session-1", CancellationToken.None);

        result.Should().BeSameAs(restoredSession);
        await agent.Received(1).DeserializeSessionAsync(
            Arg.Any<JsonElement>(),
            Arg.Any<JsonSerializerOptions>(),
            Arg.Any<CancellationToken>());
    }

    private static JsonElement ParseJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}