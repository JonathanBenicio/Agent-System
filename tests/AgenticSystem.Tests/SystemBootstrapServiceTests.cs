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
        await using var context = CreateContext(out var tenantScope);
        using (tenantScope)
        {
            var sut = CreateService(context);

            await sut.BootstrapAsync();

            (await context.Tenants.IgnoreQueryFilters().CountAsync()).Should().Be(0);
            (await context.AccessApiKeys.IgnoreQueryFilters().CountAsync()).Should().Be(0);
            (await context.DynamicAgents.IgnoreQueryFilters().CountAsync()).Should().Be(0);
            (await context.WorkflowDefinitions.IgnoreQueryFilters().CountAsync()).Should().Be(0);
        }
    }

    [Fact]
    public async Task BootstrapAsync_WithConfiguredKey_CreatesHashedAdminKeyWithoutProductSeeds()
    {
        await using var context = CreateContext(out var tenantScope);
        using (tenantScope)
        {
            const string apiKey = "explicit-test-bootstrap-secret";
            var sut = CreateService(context, apiKey);

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

    private static AgenticDbContext CreateContext(out IDisposable tenantScope)
    {
        var accessor = new TenantContextAccessor();
        tenantScope = accessor.BeginScope(new TenantContext { TenantId = "system-bootstrap" });
        var options = new DbContextOptionsBuilder<AgenticDbContext>()
            .UseInMemoryDatabase($"system-bootstrap-tests-{Guid.NewGuid():N}")
            .Options;
        var context = new AgenticDbContext(options, accessor);
        context.Database.EnsureCreated();
        return context;
    }

    private static SystemBootstrapService CreateService(AgenticDbContext context, string? apiKey = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(apiKey is null
                ? new Dictionary<string, string?>()
                : new Dictionary<string, string?> { ["AgenticSystem:AdminApiKey"] = apiKey })
            .Build();

        return new SystemBootstrapService(
            context,
            configuration,
            Substitute.For<ILogger<SystemBootstrapService>>());
    }
}
