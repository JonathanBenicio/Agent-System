using System.Text.Json;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.AgentFramework;
using FluentAssertions;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace AgenticSystem.Tests;

public class SimpleSessionStoreAdapterTests
{
    private readonly ISessionStore _sessionStore;
    private readonly ILogger<SimpleSessionStoreAdapter> _logger;

    public SimpleSessionStoreAdapterTests()
    {
        _sessionStore = Substitute.For<ISessionStore>();
        _logger = Substitute.For<ILogger<SimpleSessionStoreAdapter>>();
    }

    [Fact]
    public async Task SaveSessionAsync_WhenSessionExists_PersistsUsingStableAgentNameKey()
    {
        var sessionData = new SessionData { Id = "session-1" };
        _sessionStore.GetAsync("session-1", Arg.Any<CancellationToken>()).Returns(sessionData);
        _sessionStore.SaveAsync(sessionData, Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var agent = Substitute.For<AIAgent>();
        var frameworkSession = Substitute.For<AgentSession>();
        agent.Name.Returns("Orchestrator");
        agent.SerializeSessionAsync(
                frameworkSession,
                Arg.Any<JsonSerializerOptions>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(ParseJson("{\"messages\":[]}")));

        var sut = new SimpleSessionStoreAdapter(_sessionStore, _logger);

        await sut.SaveSessionAsync(agent, "session-1", frameworkSession, CancellationToken.None);

        sessionData.RuntimeSettings.Should().ContainKey("frameworkSessionState:orchestrator");
        sessionData.RuntimeSettings["frameworkSessionState:orchestrator"].Should().Be("{\"messages\":[]}");
        await _sessionStore.Received(1).SaveAsync(sessionData, Arg.Any<CancellationToken>());
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
        agent.DeserializeSessionAsync(
                Arg.Any<JsonElement>(),
                Arg.Any<JsonSerializerOptions>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(restoredSession));

        var sut = new SimpleSessionStoreAdapter(_sessionStore, _logger);

        var result = await sut.GetSessionAsync(agent, "session-1", CancellationToken.None);

        result.Should().BeSameAs(restoredSession);
        await agent.Received(1).DeserializeSessionAsync(
            Arg.Any<JsonElement>(),
            Arg.Any<JsonSerializerOptions>(),
            Arg.Any<CancellationToken>());
        await agent.DidNotReceive().CreateSessionAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetSessionAsync_WhenOnlyLegacyAgentIdKeyExists_CreatesNewSession()
    {
        var createdSession = Substitute.For<AgentSession>();
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
        agent.CreateSessionAsync(Arg.Any<CancellationToken>()).Returns(ValueTask.FromResult(createdSession));

        var sut = new SimpleSessionStoreAdapter(_sessionStore, _logger);

        var result = await sut.GetSessionAsync(agent, "session-1", CancellationToken.None);

        result.Should().BeSameAs(createdSession);
        await agent.DidNotReceive().DeserializeSessionAsync(
            Arg.Any<JsonElement>(),
            Arg.Any<JsonSerializerOptions>(),
            Arg.Any<CancellationToken>());
        await agent.Received(1).CreateSessionAsync(Arg.Any<CancellationToken>());
    }

    private static JsonElement ParseJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}