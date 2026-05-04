using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Core.Services;

namespace AgenticSystem.Tests.MultiTenancy;

public class TenantIsolationTests
{
    private readonly ISessionStore _store = new InMemorySessionStore();

    private static SessionData CreateSession(string tenantId, string userId, string? id = null) => new()
    {
        Id = id ?? Guid.NewGuid().ToString(),
        UserId = userId,
        TenantId = tenantId,
        StartedAt = DateTime.UtcNow
    };

    [Fact]
    public async Task GetByTenant_ReturnsOnlyMatchingTenant()
    {
        var s1 = CreateSession("tenant-a", "user-1");
        var s2 = CreateSession("tenant-b", "user-2");
        var s3 = CreateSession("tenant-a", "user-3");

        await _store.SaveAsync(s1);
        await _store.SaveAsync(s2);
        await _store.SaveAsync(s3);

        var tenantA = await _store.GetByTenantAsync("tenant-a");
        tenantA.Should().HaveCount(2);
        tenantA.Should().OnlyContain(s => s.TenantId == "tenant-a");

        var tenantB = await _store.GetByTenantAsync("tenant-b");
        tenantB.Should().HaveCount(1);
        tenantB.First().UserId.Should().Be("user-2");
    }

    [Fact]
    public async Task GetByTenant_WithUserFilter_ReturnsFiltered()
    {
        var s1 = CreateSession("tenant-x", "user-1");
        var s2 = CreateSession("tenant-x", "user-2");

        await _store.SaveAsync(s1);
        await _store.SaveAsync(s2);

        var result = await _store.GetByTenantAsync("tenant-x", userId: "user-1");
        result.Should().HaveCount(1);
        result.First().UserId.Should().Be("user-1");
    }

    [Fact]
    public async Task GetByTenant_EmptyTenant_ReturnsEmpty()
    {
        var result = await _store.GetByTenantAsync("nonexistent");
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task SessionData_DefaultsTenantId_ToDefault()
    {
        var session = new SessionData { Id = "s1", UserId = "u1" };
        session.TenantId.Should().Be(Tenant.DefaultTenantId);
    }

    [Fact]
    public async Task UserContext_DefaultsTenantId_ToDefault()
    {
        var ctx = new UserContext { UserId = "u1" };
        ctx.TenantId.Should().Be(Tenant.DefaultTenantId);
    }

    [Fact]
    public async Task TenantLimits_FreeTier_HasCorrectDefaults()
    {
        var limits = TenantLimits.FreeTier();
        limits.MaxRequestsPerMinute.Should().BeGreaterThan(0);
        limits.MaxConcurrentSessions.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task TenantLimits_Tiers_AreProgressive()
    {
        var free = TenantLimits.FreeTier();
        var pro = TenantLimits.ProTier();
        var enterprise = TenantLimits.EnterpriseTier();

        pro.MaxRequestsPerMinute.Should().BeGreaterThan(free.MaxRequestsPerMinute);
        enterprise.MaxRequestsPerMinute.Should().BeGreaterThan(pro.MaxRequestsPerMinute);
        enterprise.MaxDailyCostUsd.Should().BeGreaterThan(pro.MaxDailyCostUsd);
    }
}
