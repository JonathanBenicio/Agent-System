using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Core.Services;

namespace AgenticSystem.Tests;

public class InMemorySessionStoreTests
{
    private readonly ISessionStore _store = new InMemorySessionStore();

    private static SessionData CreateSession(string? id = null, string userId = "user-1")
        => new()
        {
            Id = id ?? Guid.NewGuid().ToString(),
            UserId = userId,
            StartedAt = DateTime.UtcNow
        };

    [Fact]
    public async Task SaveAndGet_RoundTrips()
    {
        var session = CreateSession();
        await _store.SaveAsync(session);

        var result = await _store.GetAsync(session.Id);
        result.Should().NotBeNull();
        result!.Id.Should().Be(session.Id);
        result.UserId.Should().Be(session.UserId);
    }

    [Fact]
    public async Task GetAsync_NonExistent_ReturnsNull()
    {
        var result = await _store.GetAsync("does-not-exist");
        result.Should().BeNull();
    }

    [Fact]
    public async Task ExistsAsync_ReturnsTrueForExisting()
    {
        var session = CreateSession();
        await _store.SaveAsync(session);

        (await _store.ExistsAsync(session.Id)).Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_ReturnsFalseForMissing()
    {
        (await _store.ExistsAsync("nope")).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAsync_RemovesSession()
    {
        var session = CreateSession();
        await _store.SaveAsync(session);
        await _store.DeleteAsync(session.Id);

        (await _store.ExistsAsync(session.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task GetByUserAsync_FiltersAndLimits()
    {
        for (int i = 0; i < 5; i++)
            await _store.SaveAsync(CreateSession(userId: "user-A"));

        await _store.SaveAsync(CreateSession(userId: "user-B"));

        var results = await _store.GetByUserAsync("user-A", 3);
        results.Should().HaveCount(3);
        results.Should().AllSatisfy(s => s.UserId.Should().Be("user-A"));
    }

    [Fact]
    public async Task SaveAsync_Overwrites_ExistingSession()
    {
        var session = CreateSession();
        session.Events.Add(new AgentEvent { AgentName = "A1" });
        await _store.SaveAsync(session);

        session.Events.Add(new AgentEvent { AgentName = "A2" });
        await _store.SaveAsync(session);

        var result = await _store.GetAsync(session.Id);
        result!.Events.Should().HaveCount(2);
    }
}
