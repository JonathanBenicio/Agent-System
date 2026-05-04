using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Core.Services;

namespace AgenticSystem.Tests;

public class SessionManagerTests
{
    private readonly ISessionStore _store;
    private readonly ISessionConsolidator _consolidator;
    private readonly ILogger<SessionManager> _logger;
    private readonly SessionManager _sut;

    public SessionManagerTests()
    {
        _store = new InMemorySessionStore();
        _consolidator = Substitute.For<ISessionConsolidator>();
        _logger = Substitute.For<ILogger<SessionManager>>();
        _sut = new SessionManager(_store, _consolidator, _logger);
    }

    [Fact]
    public async Task StartSessionAsync_ReturnsNonEmptySessionId()
    {
        var context = new UserContext { UserId = "user1" };
        var sessionId = await _sut.StartSessionAsync(context);

        sessionId.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task AddEventAsync_AndGetRecentEventsAsync_ReturnsEvents()
    {
        var context = new UserContext { UserId = "user1" };
        var sessionId = await _sut.StartSessionAsync(context);

        var agentEvent = new AgentEvent
        {
            SessionId = sessionId,
            AgentName = "TestAgent",
            AgentTier = AgentTier.Support,
            UserInput = "test input",
            AgentResponse = "test response"
        };

        await _sut.AddEventAsync(sessionId, agentEvent);
        var events = await _sut.GetRecentEventsAsync(sessionId, 10);

        events.Should().HaveCount(1);
        events.First().AgentName.Should().Be("TestAgent");
    }

    [Fact]
    public async Task ConsolidateSessionAsync_DoesNotThrow()
    {
        var context = new UserContext { UserId = "user1" };
        var sessionId = await _sut.StartSessionAsync(context);

        await _sut.Invoking(s => s.ConsolidateSessionAsync(sessionId))
            .Should().NotThrowAsync();
    }

    [Fact]
    public async Task EndSessionAsync_DoesNotThrow()
    {
        var context = new UserContext { UserId = "user1" };
        var sessionId = await _sut.StartSessionAsync(context);

        await _sut.Invoking(s => s.EndSessionAsync(sessionId))
            .Should().NotThrowAsync();
    }

    [Fact]
    public async Task GetRecentEventsAsync_WithInvalidSession_ReturnsEmpty()
    {
        var events = await _sut.GetRecentEventsAsync("nonexistent", 10);
        events.Should().BeEmpty();
    }
}
