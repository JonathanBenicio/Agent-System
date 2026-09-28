using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Core.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;
using FluentAssertions;
using Xunit;

namespace AgenticSystem.Tests.MultiTenancy;

public class TenantResolverTests
{
    private readonly ITenantStore _store = Substitute.For<ITenantStore>();
    private readonly ILogger<TenantResolver> _logger = Substitute.For<ILogger<TenantResolver>>();
    private readonly ITenantResolver _resolver;

    public TenantResolverTests()
    {
        _resolver = new TenantResolver(_store, _logger);
    }

    [Fact]
    public async Task ResolveAsync_ValidTenant_ReturnsTenantContext()
    {
        var tenant = new Tenant
        {
            Id = "admin",
            Name = "Admin Corp",
            Slug = "admin-corp",
            Plan = TenantPlan.Pro,
            Limits = TenantLimits.ProTier(),
            IsActive = true
        };
        _store.GetByIdAsync("admin", Arg.Any<CancellationToken>()).Returns(tenant);

        var ctx = await _resolver.ResolveAsync("admin");

        ctx.Should().NotBeNull();
        ctx!.TenantId.Should().Be("admin");
        ctx.IsAuthenticated.Should().BeTrue();
    }

    [Fact]
    public async Task ResolveAsync_UnknownTenantId_ReturnsNull()
    {
        _store.GetByIdAsync("nonexistent-tenant", Arg.Any<CancellationToken>()).Returns((Tenant?)null);
        var ctx = await _resolver.ResolveAsync("nonexistent-tenant");
        ctx.Should().BeNull();
    }

    [Fact]
    public async Task ResolveAsync_InactiveTenant_ReturnsNull()
    {
        var tenant = new Tenant
        {
            Id = "inactive-1",
            Name = "Inactive Corp",
            Slug = "inactive-corp",
            Plan = TenantPlan.Pro,
            Limits = TenantLimits.ProTier(),
            IsActive = false
        };
        _store.GetByIdAsync("inactive-1", Arg.Any<CancellationToken>()).Returns(tenant);

        var ctx = await _resolver.ResolveAsync("inactive-1");
        ctx.Should().BeNull();
    }

    [Fact]
    public async Task ResolveAsync_ActiveTenant_MapsCorrectly()
    {
        var tenant = new Tenant
        {
            Id = "tenant-pro",
            Name = "Pro Corp",
            Slug = "pro-corp",
            Plan = TenantPlan.Enterprise,
            Limits = TenantLimits.EnterpriseTier(),
            IsActive = true
        };
        _store.GetByIdAsync("tenant-pro", Arg.Any<CancellationToken>()).Returns(tenant);

        var ctx = await _resolver.ResolveAsync("tenant-pro");

        ctx.Should().NotBeNull();
        ctx!.TenantId.Should().Be("tenant-pro");
        ctx.TenantName.Should().Be("Pro Corp");
        ctx.Plan.Should().Be(TenantPlan.Enterprise);
        ctx.IsAuthenticated.Should().BeTrue();
    }
}
