using AgenticSystem.Core.Models;
using AgenticSystem.Core.Services;
using FluentAssertions;

namespace AgenticSystem.Tests;

public class SystemOperationContextAccessorTests
{
    [Fact]
    public void BeginScope_ProvidesCapabilityWithoutTenantIdentity_AndRestoresNestedScope()
    {
        var accessor = new SystemOperationContextAccessor();

        accessor.Invoking(value => value.Require(SystemOperationKind.ProcessOutbox))
            .Should().Throw<InvalidOperationException>();

        using (accessor.BeginScope(SystemOperationKind.ProcessOutbox))
        {
            var outbox = accessor.Require(SystemOperationKind.ProcessOutbox);
            outbox.Operation.Should().Be(SystemOperationKind.ProcessOutbox);
            outbox.OperationId.Should().NotBeNullOrWhiteSpace();
            typeof(SystemOperationContext).GetProperty("TenantId").Should().BeNull();
            accessor.Invoking(value => value.Require(SystemOperationKind.PlatformQuotaSync))
                .Should().Throw<InvalidOperationException>();

            using (accessor.BeginScope(SystemOperationKind.PlatformQuotaSync))
            {
                accessor.Require(SystemOperationKind.PlatformQuotaSync).Operation.Should().Be(SystemOperationKind.PlatformQuotaSync);
            }

            accessor.Require(SystemOperationKind.ProcessOutbox).Operation.Should().Be(SystemOperationKind.ProcessOutbox);
        }

        accessor.Current.Should().BeNull();
    }

    [Fact]
    public void SystemScope_DoesNotChangeTenantContext()
    {
        var tenantAccessor = new TenantContextAccessor();
        var systemAccessor = new SystemOperationContextAccessor();

        using (tenantAccessor.BeginScope(new TenantContext { TenantId = "tenant-a" }))
        using (systemAccessor.BeginScope(SystemOperationKind.PlatformCatalogRead))
        {
            tenantAccessor.CurrentTenantId.Should().Be("tenant-a");
            systemAccessor.Require(SystemOperationKind.PlatformCatalogRead).Operation.Should().Be(SystemOperationKind.PlatformCatalogRead);
        }

        systemAccessor.Current.Should().BeNull();
        tenantAccessor.Invoking(value => value.CurrentTenantId).Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData("platform")]
    [InlineData("system-background")]
    [InlineData("PLATFORM")]
    public void TenantContextAccessor_RejectsReservedSystemIdentifiers(string tenantId)
    {
        var tenantAccessor = new TenantContextAccessor();

        tenantAccessor.Invoking(value => value.BeginScope(new TenantContext { TenantId = tenantId }))
            .Should().Throw<ArgumentException>();
    }
}
