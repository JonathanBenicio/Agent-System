using AgenticSystem.Core.Models;
using AgenticSystem.Core.Services;
using FluentAssertions;
using Xunit;

namespace AgenticSystem.Tests;

public sealed class FidesTenantPolicyStoreTests
{
    [Fact]
    public async Task PoliciesDefaultEnabledAndAreIsolatedPerTenant()
    {
        var accessor = new TenantContextAccessor();
        var store = new InMemoryFidesTenantPolicyStore(accessor);
        FidesTenantPolicy tenantAPolicy;
        using (accessor.BeginScope(new TenantContext { TenantId = "tenant-a" }))
        {
            var defaults = await store.GetAsync();
            defaults.EnabledDetectors.Values.Should().OnlyContain(enabled => enabled);

            tenantAPolicy = await store.SaveAsync(
                new Dictionary<string, bool> { [FidesDetectorCatalog.Email] = false },
                "owner-a");
            tenantAPolicy.EnabledDetectors[FidesDetectorCatalog.Email].Should().BeFalse();
            tenantAPolicy.EnabledDetectors[FidesDetectorCatalog.CredentialToken].Should().BeTrue();
            tenantAPolicy.Version.Should().Be(1);
        }

        using (accessor.BeginScope(new TenantContext { TenantId = "tenant-b" }))
        {
            var otherTenantPolicy = await store.GetAsync();
            otherTenantPolicy.EnabledDetectors.Values.Should().OnlyContain(enabled => enabled);
            otherTenantPolicy.Version.Should().Be(0);
        }

        using (accessor.BeginScope(new TenantContext { TenantId = "tenant-a" }))
        {
            var act = () => store.SaveAsync(
                new Dictionary<string, bool> { [FidesDetectorCatalog.CredentialToken] = false },
                "owner-a");

            await act.Should().ThrowAsync<ArgumentException>()
                .WithMessage("*cannot be disabled*");
        }
    }
}
