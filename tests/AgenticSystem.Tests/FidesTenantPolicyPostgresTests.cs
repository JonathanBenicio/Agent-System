using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Core.Services;
using AgenticSystem.Infrastructure.Persistence;
using AgenticSystem.Infrastructure.Persistence.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pgvector.EntityFrameworkCore;
using Xunit;

namespace AgenticSystem.Tests;

public sealed class FidesTenantPolicyPostgresTests
{
    [RequiresPostgresFact]
    public async Task PolicyPersistsAndRemainsTenantIsolated()
    {
        var connectionString = GetIsolatedConnectionString();
        var accessor = new TenantContextAccessor();
        var options = new DbContextOptionsBuilder<AgenticDbContext>()
            .UseNpgsql(connectionString, npgsql =>
            {
                npgsql.UseVector();
                npgsql.MigrationsHistoryTable("__ef_migrations_history");
            })
            .Options;
        await using (var setup = new AgenticDbContext(options, accessor))
            await setup.Database.MigrateAsync();

        var store = new PostgresFidesTenantPolicyStore(new PolicyDbContextFactory(options, accessor), accessor);
        var tenantA = $"fides-policy-{Guid.NewGuid():N}";
        using (accessor.BeginScope(new TenantContext { TenantId = tenantA }))
        {
            var saved = await store.SaveAsync(
                new Dictionary<string, bool> { [FidesDetectorCatalog.Email] = false },
                "test-owner");
            saved.Version.Should().Be(1);
            saved.EnabledDetectors[FidesDetectorCatalog.Email].Should().BeFalse();
        }

        using (accessor.BeginScope(new TenantContext { TenantId = tenantA }))
        {
            var persisted = await store.GetAsync();
            persisted.Version.Should().Be(1);
            persisted.EnabledDetectors[FidesDetectorCatalog.Email].Should().BeFalse();
            persisted.EnabledDetectors[FidesDetectorCatalog.CredentialToken].Should().BeTrue();
        }

        using (accessor.BeginScope(new TenantContext { TenantId = "fides-other-tenant" }))
        {
            var otherTenant = await store.GetAsync();
            otherTenant.Version.Should().Be(0);
            otherTenant.EnabledDetectors.Values.Should().OnlyContain(enabled => enabled);
        }

        await using var cleanup = new AgenticDbContext(options, accessor);
        await cleanup.FidesTenantPolicies.IgnoreQueryFilters()
            .Where(policy => policy.TenantId == tenantA)
            .ExecuteDeleteAsync();
    }

    private static string GetIsolatedConnectionString()
    {
        var connectionString = Environment.GetEnvironmentVariable("AGENTIC_TEST_POSTGRES");
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        var target = new NpgsqlConnectionStringBuilder(connectionString);
        if (target.Host != "127.0.0.1" || target.Port != 55432 ||
            string.IsNullOrWhiteSpace(target.Database) ||
            !target.Database.StartsWith("review_pr152_", StringComparison.Ordinal) || target.Username != "validation")
            throw new InvalidOperationException("FIDES PostgreSQL test only accepts an isolated review_pr152_* database on backend-validation Compose port 55432.");

        var efConnectionString = Environment.GetEnvironmentVariable("AGENTIC_EF_CONNECTION");
        ArgumentException.ThrowIfNullOrWhiteSpace(efConnectionString);
        var efTarget = new NpgsqlConnectionStringBuilder(efConnectionString);
        if (efTarget.Host != target.Host || efTarget.Port != target.Port ||
            efTarget.Database != target.Database || efTarget.Username != target.Username ||
            efTarget.Password != target.Password)
            throw new InvalidOperationException("The PostgreSQL test and EF migration connection strings must target the same isolated database.");

        return connectionString;
    }

    private sealed class PolicyDbContextFactory : IDbContextFactory<AgenticDbContext>
    {
        private readonly DbContextOptions<AgenticDbContext> _options;
        private readonly ITenantContextAccessor _tenantAccessor;

        public PolicyDbContextFactory(DbContextOptions<AgenticDbContext> options, ITenantContextAccessor tenantAccessor)
        {
            _options = options;
            _tenantAccessor = tenantAccessor;
        }

        public AgenticDbContext CreateDbContext() => new(_options, _tenantAccessor);

        public Task<AgenticDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDbContext());
    }
}
