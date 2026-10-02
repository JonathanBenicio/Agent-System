using AgenticSystem.Core.Exceptions;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgenticSystem.Tests;

public sealed class SessionCreationCeilingTests
{
    [Fact]
    public async Task ActiveCount_IncludesOldActiveSessionsBeyondRecentEndedHistory()
    {
        var store = new InMemorySessionStore();
        await store.SaveAsync(new SessionData
        {
            Id = "active", UserId = "u", TenantId = "a", StartedAt = DateTime.UtcNow.AddDays(-1)
        });
        for (var i = 0; i < 50; i++)
            await store.SaveAsync(new SessionData
            {
                Id = $"ended-{i}", UserId = "u", TenantId = "a",
                StartedAt = DateTime.UtcNow.AddMinutes(-i), EndedAt = DateTime.UtcNow
            });
        (await store.CountActiveAsync("a")).Should().Be(1);
        (await store.TryCreateAsync(new SessionData { Id = "new", UserId = "u", TenantId = "a" }, 1))
            .Should().BeFalse();
    }

    [Fact]
    public async Task AtomicCreation_ThirtyConcurrentRequestsReserveOnlyThreeSlotsPerTenant()
    {
        var store = new InMemorySessionStore();
        var manager = Manager(store, 3);
        var results = await Task.WhenAll(Enumerable.Range(0, 30).Select(async i =>
        {
            try
            {
                return await manager.StartSessionAsync(new UserContext { UserId = $"u-{i}", TenantId = "a" });
            }
            catch (QuotaExceededException) { return null; }
        }));
        results.Count(id => id is not null).Should().Be(3);
        (await store.CountActiveAsync("a")).Should().Be(3);
        await manager.StartSessionAsync(new UserContext { UserId = "b-u", TenantId = "b" });
        (await store.CountActiveAsync("b")).Should().Be(1);
    }

    [Fact]
    public async Task ResumeAtCeiling_PreservesOwnerAndDoesNotCreateAnotherSlot()
    {
        var store = new InMemorySessionStore();
        var manager = Manager(store, 1);
        var user = new UserContext { UserId = "u", TenantId = "a" };
        var id = await manager.StartSessionAsync(user);
        (await manager.StartSessionAsync(user, id)).Should().Be(id);
        (await store.CountActiveAsync("a")).Should().Be(1);
        await ((Func<Task>)(() => manager.StartSessionAsync(user)))
            .Should().ThrowAsync<QuotaExceededException>();
        await ((Func<Task>)(() => manager.StartSessionAsync(new UserContext { UserId = "other", TenantId = "a" }, id)))
            .Should().ThrowAsync<UnauthorizedAccessException>();
        await manager.EndSessionAsync(id);
        await manager.StartSessionAsync(user);
        (await store.CountActiveAsync("a")).Should().Be(1);
    }

    [Fact]
    public async Task TenantConfigurationCannotExceedPlanSessionCeiling()
    {
        var store = new InMemorySessionStore();
        var manager = Manager(store, 100);
        var user = new UserContext { UserId = "u", TenantId = "a" };
        for (var i = 0; i < TenantLimits.FreeTier().MaxConcurrentSessions; i++)
            await manager.StartSessionAsync(user);
        await ((Func<Task>)(() => manager.StartSessionAsync(user))).Should().ThrowAsync<QuotaExceededException>();
    }

    private static SessionManager Manager(ISessionStore store, int limit)
    {
        var tenants = Substitute.For<ITenantStore>();
        tenants.GetByIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call =>
            new Tenant { Id = call.ArgAt<string>(0), Plan = TenantPlan.Free,
                Limits = new TenantLimits { MaxConcurrentSessions = limit }, IsActive = true });
        return new SessionManager(store, Substitute.For<ISessionConsolidator>(),
            NullLogger<SessionManager>.Instance, tenantStore: tenants);
    }
}
