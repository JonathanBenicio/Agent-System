using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Core.Services;

namespace AgenticSystem.Tests.MultiTenancy;

public class InMemoryTenantStoreTests
{
    private readonly ITenantStore _store = new InMemoryTenantStore();

    private static Tenant CreateTenant(string? id = null, string name = "Test Corp") => new()
    {
        Id = id ?? Guid.NewGuid().ToString(),
        Name = name,
        Slug = name.ToLowerInvariant().Replace(" ", "-"),
        Plan = TenantPlan.Pro,
        Limits = TenantLimits.ProTier(),
        IsActive = true
    };

    [Fact]
    public async Task DefaultTenant_ExistsOnConstruction()
    {
        var exists = await _store.ExistsAsync(Tenant.DefaultTenantId);
        exists.Should().BeTrue();

        var tenant = await _store.GetByIdAsync(Tenant.DefaultTenantId);
        tenant.Should().NotBeNull();
        tenant!.Name.Should().Be("Default");
    }

    [Fact]
    public async Task SaveAndGetById_RoundTrips()
    {
        var tenant = CreateTenant();
        await _store.SaveAsync(tenant);

        var result = await _store.GetByIdAsync(tenant.Id);
        result.Should().NotBeNull();
        result!.Id.Should().Be(tenant.Id);
        result.Name.Should().Be(tenant.Name);
    }

    [Fact]
    public async Task GetBySlug_ReturnsTenant()
    {
        var tenant = CreateTenant(name: "Acme Inc");
        await _store.SaveAsync(tenant);

        var result = await _store.GetBySlugAsync("acme-inc");
        result.Should().NotBeNull();
        result!.Id.Should().Be(tenant.Id);
    }

    [Fact]
    public async Task GetBySlug_ReturnsNull_WhenNotFound()
    {
        var result = await _store.GetBySlugAsync("nonexistent");
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAll_ReturnsAllTenants()
    {
        var t1 = CreateTenant(name: "Tenant A");
        var t2 = CreateTenant(name: "Tenant B");
        await _store.SaveAsync(t1);
        await _store.SaveAsync(t2);

        var all = await _store.GetAllAsync();
        all.Should().HaveCountGreaterThanOrEqualTo(3); // default + t1 + t2
    }

    [Fact]
    public async Task Delete_RemovesTenant()
    {
        var tenant = CreateTenant();
        await _store.SaveAsync(tenant);
        await _store.DeleteAsync(tenant.Id);

        var exists = await _store.ExistsAsync(tenant.Id);
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task Save_UpdatesExistingTenant()
    {
        var tenant = CreateTenant();
        await _store.SaveAsync(tenant);

        tenant.Name = "Updated Name";
        await _store.SaveAsync(tenant);

        var result = await _store.GetByIdAsync(tenant.Id);
        result!.Name.Should().Be("Updated Name");
    }
}
