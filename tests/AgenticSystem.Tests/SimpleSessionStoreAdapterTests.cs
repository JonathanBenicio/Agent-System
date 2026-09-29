#pragma warning disable MAAI001 // The tests exercise the experimental MAF session-store contract used by the backend.

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
    private readonly ISessionStore _sessionStore = Substitute.For<ISessionStore>();
    private readonly ILogger<SimpleSessionStoreAdapter> _logger = Substitute.For<ILogger<SimpleSessionStoreAdapter>>();

    [Fact]
    public async Task SaveSessionAsync_PersistsUnderAllSessionKeyPartitions()
    {
        var sessionData = OwnedSession();
        _sessionStore.GetAsync("session-1", Arg.Any<CancellationToken>()).Returns(sessionData);
        var agent = CreateAgent("Orchestrator");
        var sut = new SimpleSessionStoreAdapter(_sessionStore, _logger);
        var key = new AgentSessionStoreKey("session-1")
            .WithPartition("isolation", "tenant-a:user-1")
            .WithPartition("room", "room-9");

        await sut.SaveSessionAsync(agent, key, Substitute.For<AgentSession>(), CancellationToken.None);

        var scopedState = sessionData.RuntimeSettings.Single(item => item.Key.StartsWith(
            "frameworkSessionState:orchestrator:scope:", StringComparison.Ordinal));
        scopedState.Value.Should().Be("{\"messages\":[]}");
        await _sessionStore.Received(1).SaveAsync(sessionData, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetSessionAsync_DoesNotReturnStateFromDifferentPartition()
    {
        var sessionData = OwnedSession();
        _sessionStore.GetAsync("session-1", Arg.Any<CancellationToken>()).Returns(sessionData);
        var agent = CreateAgent("Orchestrator");
        var sut = new SimpleSessionStoreAdapter(_sessionStore, _logger);
        var keyA = new AgentSessionStoreKey("session-1")
            .WithPartition("isolation", "tenant-a:user-1")
            .WithPartition("room", "room-a");
        var keyB = new AgentSessionStoreKey("session-1")
            .WithPartition("isolation", "tenant-a:user-1")
            .WithPartition("room", "room-b");
        await sut.SaveSessionAsync(agent, keyA, Substitute.For<AgentSession>(), CancellationToken.None);

        var otherPartition = await sut.GetSessionAsync(agent, keyB, CancellationToken.None);
        var originalPartition = await sut.GetSessionAsync(agent, keyA, CancellationToken.None);

        otherPartition.Should().BeNull();
        originalPartition.Should().NotBeNull();
        originalPartition.Should().BeSameAs(agent.DeserializedSession);
    }

    [Fact]
    public async Task GetSessionAsync_MigratesLegacyStateOnlyForMatchingOwner()
    {
        var sessionData = OwnedSession();
        sessionData.RuntimeSettings["frameworkSessionState:orchestrator"] = "{\"messages\":[]}";
        _sessionStore.GetAsync("session-1", Arg.Any<CancellationToken>()).Returns(sessionData);
        var agent = CreateAgent("Orchestrator");
        var sut = new SimpleSessionStoreAdapter(_sessionStore, _logger);
        var key = new AgentSessionStoreKey("session-1").WithPartition("isolation", "tenant-a:user-1");

        var restored = await sut.GetSessionAsync(agent, key, CancellationToken.None);

        restored.Should().BeSameAs(agent.DeserializedSession);
        sessionData.RuntimeSettings.Should().NotContainKey("frameworkSessionState:orchestrator");
        sessionData.RuntimeSettings.Keys.Should().ContainSingle(name => name.StartsWith(
            "frameworkSessionState:orchestrator:scope:", StringComparison.Ordinal));
        await _sessionStore.Received(1).SaveAsync(sessionData, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetSessionAsync_RejectsDifferentOwnerBeforeReadingMafState()
    {
        var sessionData = OwnedSession();
        sessionData.RuntimeSettings["frameworkSessionState:orchestrator"] = "{\"messages\":[]}";
        _sessionStore.GetAsync("session-1", Arg.Any<CancellationToken>()).Returns(sessionData);
        var agent = CreateAgent("Orchestrator");
        var sut = new SimpleSessionStoreAdapter(_sessionStore, _logger);
        var key = new AgentSessionStoreKey("session-1").WithPartition("isolation", "tenant-a:user-2");

        var act = () => sut.GetSessionAsync(agent, key, CancellationToken.None).AsTask();

        await act.Should().ThrowAsync<InvalidOperationException>();
        agent.DeserializeCalls.Should().Be(0);
        sessionData.RuntimeSettings.Should().ContainKey("frameworkSessionState:orchestrator");
    }

    private static SessionData OwnedSession() => new()
    {
        Id = "session-1",
        TenantId = "tenant-a",
        UserId = "user-1"
    };

    private static TestAgent CreateAgent(string name)
    {
        var agent = Substitute.For<TestAgent>();
        agent.Name.Returns(name);
        agent.SerializeSessionAsync(
                Arg.Any<AgentSession>(),
                Arg.Any<JsonSerializerOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(ParseJson("{\"messages\":[]}")));
        agent.DeserializeSessionAsync(
                Arg.Any<JsonElement>(),
                Arg.Any<JsonSerializerOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                agent.DeserializeCalls++;
                return ValueTask.FromResult(agent.DeserializedSession);
            });
        return agent;
    }

    private static JsonElement ParseJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    public abstract class TestAgent : AIAgent
    {
        public AgentSession DeserializedSession { get; } = Substitute.For<AgentSession>();
        public int DeserializeCalls { get; set; }
    }
}
