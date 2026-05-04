using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgenticSystem.Tests;

public class EfSessionStoreTests : IDisposable
{
    private readonly AgenticDbContext _db;
    private readonly ISessionStore _store;

    public EfSessionStoreTests()
    {
        var options = new DbContextOptionsBuilder<AgenticDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _db = new AgenticDbContext(options);
        _store = new EfSessionStore(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    private static SessionData CreateSession(string? id = null, string userId = "user-1", string tenantId = "tenant-1")
        => new()
        {
            Id = id ?? Guid.NewGuid().ToString(),
            UserId = userId,
            TenantId = tenantId,
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
    public async Task GetByTenantAsync_FiltersByTenant()
    {
        await _store.SaveAsync(CreateSession(tenantId: "t1"));
        await _store.SaveAsync(CreateSession(tenantId: "t1"));
        await _store.SaveAsync(CreateSession(tenantId: "t2"));

        var results = await _store.GetByTenantAsync("t1");
        results.Should().HaveCount(2);
        results.Should().AllSatisfy(s => s.TenantId.Should().Be("t1"));
    }

    [Fact]
    public async Task GetByTenantAsync_FiltersByTenantAndUser()
    {
        await _store.SaveAsync(CreateSession(tenantId: "t1", userId: "u1"));
        await _store.SaveAsync(CreateSession(tenantId: "t1", userId: "u2"));
        await _store.SaveAsync(CreateSession(tenantId: "t1", userId: "u1"));

        var results = await _store.GetByTenantAsync("t1", userId: "u1");
        results.Should().HaveCount(2);
        results.Should().AllSatisfy(s =>
        {
            s.TenantId.Should().Be("t1");
            s.UserId.Should().Be("u1");
        });
    }

    [Fact]
    public async Task SaveAsync_Updates_ExistingSession()
    {
        var session = CreateSession(id: "fixed-id");
        session.Events.Add(new AgentEvent { Id = Guid.NewGuid().ToString(), AgentName = "A1" });
        await _store.SaveAsync(session);

        // Detach to simulate a new context scenario
        _db.ChangeTracker.Clear();

        var updated = CreateSession(id: "fixed-id");
        updated.Events.Add(new AgentEvent { Id = Guid.NewGuid().ToString(), AgentName = "A1" });
        updated.Events.Add(new AgentEvent { Id = Guid.NewGuid().ToString(), AgentName = "A2" });
        await _store.SaveAsync(updated);

        _db.ChangeTracker.Clear();

        var result = await _store.GetAsync("fixed-id");
        result.Should().NotBeNull();
        result!.Events.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetByUserAsync_OrdersByStartedAtDescending()
    {
        var older = CreateSession(userId: "user-X");
        older.StartedAt = DateTime.UtcNow.AddHours(-2);
        await _store.SaveAsync(older);

        var newer = CreateSession(userId: "user-X");
        newer.StartedAt = DateTime.UtcNow;
        await _store.SaveAsync(newer);

        var results = await _store.GetByUserAsync("user-X");
        results.First().StartedAt.Should().BeOnOrAfter(results.Last().StartedAt);
    }
}
