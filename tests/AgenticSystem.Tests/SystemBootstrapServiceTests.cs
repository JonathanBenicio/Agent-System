using System.Security.Cryptography;
using System.Text;
using AgenticSystem.Core.Models;
using AgenticSystem.Core.Services;
using AgenticSystem.Infrastructure.Persistence;
using AgenticSystem.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace AgenticSystem.Tests;

public sealed class SystemBootstrapServiceTests
{
    [Fact]
    public async Task BootstrapAsync_WithoutConfiguredKey_LeavesDatabaseUnprovisioned()
    {
        await using var context = CreateContext(out var tenantScope, out var tenantAccessor, tenantId: null);
        using (tenantScope)
        {
            var sut = CreateService(context, tenantAccessor);

            await sut.BootstrapAsync();

            (await context.Tenants.IgnoreQueryFilters().CountAsync()).Should().Be(0);
            (await context.AccessApiKeys.IgnoreQueryFilters().CountAsync()).Should().Be(0);
            (await context.DynamicAgents.IgnoreQueryFilters().CountAsync()).Should().Be(0);
            (await context.WorkflowDefinitions.IgnoreQueryFilters().CountAsync()).Should().Be(0);
        }
    }

    [Fact]
    public async Task BootstrapAsync_WhenTenantAlreadyExists_DoesNotRequireBootstrapKey()
    {
        await using var context = CreateContext(out var tenantScope, out var tenantAccessor);
        using (tenantScope)
        {
            context.Tenants.Add(new Tenant
            {
                Id = "existing-tenant",
                Name = "Existing Tenant",
                Slug = "existing-tenant",
                Plan = TenantPlan.Free,
                Limits = TenantLimits.FreeTier(),
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();

            var sut = CreateService(context, tenantAccessor);

            await sut.BootstrapAsync();

            (await context.Tenants.IgnoreQueryFilters().CountAsync()).Should().Be(1);
            (await context.AccessApiKeys.IgnoreQueryFilters().CountAsync()).Should().Be(0);
        }
    }

    [Fact]
    public async Task BootstrapAsync_WithConfiguredKey_CreatesHashedAdminKeyWithoutProductSeeds()
    {
        await using var context = CreateContext(out var tenantScope, out var tenantAccessor);
        using (tenantScope)
        {
            const string apiKey = "explicit-test-bootstrap-secret";
            var sut = CreateService(context, tenantAccessor, apiKey);

            await sut.BootstrapAsync();

            var tenant = await context.Tenants.IgnoreQueryFilters().SingleAsync();
            tenant.Id.Should().Be("admin");
            var key = await context.AccessApiKeys.IgnoreQueryFilters().SingleAsync();
            key.TenantId.Should().Be(tenant.Id);
            key.KeyHash.Should().Be(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(apiKey))).ToLowerInvariant());
            var membership = await context.TenantMemberships.IgnoreQueryFilters().SingleAsync();
            membership.TenantId.Should().Be(tenant.Id);
            membership.Role.Should().Be("Admin");
            (await context.DynamicAgents.IgnoreQueryFilters().CountAsync()).Should().Be(0);
            (await context.WorkflowDefinitions.IgnoreQueryFilters().CountAsync()).Should().Be(0);
        }
    }

    private static AgenticDbContext CreateContext(
        out IDisposable tenantScope,
        out TenantContextAccessor accessor,
        string? tenantId = "admin")
    {
        accessor = new TenantContextAccessor();
        tenantScope = tenantId is null
            ? EmptyScope.Instance
            : accessor.BeginScope(new TenantContext { TenantId = tenantId });
        var options = new DbContextOptionsBuilder<AgenticDbContext>()
            .UseInMemoryDatabase($"system-bootstrap-tests-{Guid.NewGuid():N}")
            .Options;
        var context = new AgenticDbContext(options, accessor);
        context.Database.EnsureCreated();
        return context;
    }

    private sealed class EmptyScope : IDisposable
    {
        public static EmptyScope Instance { get; } = new();
        public void Dispose() { }
    }

    private static SystemBootstrapService CreateService(AgenticDbContext context, TenantContextAccessor tenantAccessor, string? apiKey = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(apiKey is null
                ? new Dictionary<string, string?>()
                : new Dictionary<string, string?> { ["AgenticSystem:AdminApiKey"] = apiKey })
            .Build();

        return new SystemBootstrapService(
            context,
            configuration,
            Substitute.For<ILogger<SystemBootstrapService>>(),
            new SystemOperationContextAccessor(),
            tenantAccessor);
    }
}
