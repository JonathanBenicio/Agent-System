using AgenticSystem.Core.Models;
using AgenticSystem.Core.Services;
using FluentAssertions;

namespace AgenticSystem.Tests.MultiTenancy;

public sealed class InMemoryTenantStoreTests
{
    [Fact]
    public async Task StoreStartsWithoutSyntheticDefaultTenant()
    {
        var store = new InMemoryTenantStore();

        (await store.GetAllAsync()).Should().BeEmpty();
        (await store.GetByIdAsync("default")).Should().BeNull();
        (await store.ExistsAsync("default")).Should().BeFalse();
    }

    [Theory]
    [InlineData("default")]
    [InlineData("platform")]
    [InlineData("system-background")]
    [InlineData("system-devui")]
    public async Task StoreRejectsReservedIdentifiers(string tenantId)
    {
        var store = new InMemoryTenantStore();
        var tenant = new Tenant { Id = tenantId, Name = "Reserved", Slug = tenantId };

        var act = () => store.SaveAsync(tenant);

        await act.Should().ThrowAsync<ArgumentException>();
        (await store.GetAllAsync()).Should().BeEmpty();
    }
}
