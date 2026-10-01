using AgenticSystem.Core.Models;
using AgenticSystem.Core.Services;
using FluentAssertions;

namespace AgenticSystem.Tests.MultiTenancy;

public sealed class TenantContextAccessorTests
{
    [Theory]
    [InlineData("default")]
    [InlineData("platform")]
    [InlineData("system-background")]
    [InlineData("system-devui")]
    [InlineData(" PLATFORM ")]
    public void BeginScope_ReservedSystemIdentifier_Throws(string tenantId)
    {
        var accessor = new TenantContextAccessor();

        var act = () => accessor.BeginScope(new TenantContext { TenantId = tenantId });

        act.Should().Throw<ArgumentException>().WithMessage("*not valid tenant IDs*");
    }

    [Fact]
    public void BeginScope_RequiresTenantId()
    {
        var accessor = new TenantContextAccessor();

        var act = () => accessor.BeginScope(new TenantContext { TenantId = " " });

        act.Should().Throw<ArgumentException>().WithMessage("*real TenantId is required*");
    }

    [Fact]
    public void RequireCurrentTenant_RejectsDifferentRequestedTenant()
    {
        var accessor = new TenantContextAccessor();
        using var scope = accessor.BeginScope(new TenantContext { TenantId = "tenant-a" });

        var act = () => TenantContextPolicy.RequireCurrentTenant(accessor, "tenant-b");

        act.Should().Throw<InvalidOperationException>().WithMessage("*does not match the active tenant context*");
    }

    [Fact]
    public void RequireCurrentTenant_ReturnsTheActiveRealTenant()
    {
        var accessor = new TenantContextAccessor();
        using var scope = accessor.BeginScope(new TenantContext { TenantId = "tenant-a" });

        TenantContextPolicy.RequireCurrentTenant(accessor, "tenant-a").Should().Be("tenant-a");
    }
}
