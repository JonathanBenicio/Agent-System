using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Core.Services;
using AgenticSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Pgvector.EntityFrameworkCore;

namespace AgenticSystem.Tests;

public sealed class TenantQuotaPostgresPersistenceTests
{
    [ReviewSessionPostgresFact]
    public async Task UsageSurvivesRepositoryRecreationAndRemainsTenantScoped()
    {
        var connectionString = Environment.GetEnvironmentVariable("AGENTIC_REVIEW_POSTGRES")
            ?? throw new InvalidOperationException("AGENTIC_REVIEW_POSTGRES is required.");
        var target = new NpgsqlConnectionStringBuilder(connectionString);
        if (target.Host != "127.0.0.1" || target.Port != 55432 || target.Username != "validation" ||
            string.IsNullOrWhiteSpace(target.Database) || !target.Database.StartsWith("review_pr152_", StringComparison.Ordinal))
            throw new InvalidOperationException("Quota persistence tests require an isolated review_pr152_* database on Compose port 55432.");

        var tenantAccessor = new TenantContextAccessor();
        var options = new DbContextOptionsBuilder<AgenticDbContext>()
            .UseNpgsql(target.ConnectionString, postgres =>
            {
                postgres.UseVector();
                postgres.MigrationsHistoryTable("__ef_migrations_history");
            }).Options;
        var factory = new QuotaDbContextFactory(options, tenantAccessor);
        var tenantId = $"quota-persist-{Guid.NewGuid():N}";
        using var tenantScope = tenantAccessor.BeginScope(new TenantContext { TenantId = tenantId });

        await using (var setup = factory.CreateDbContext())
            await setup.Database.MigrateAsync();

        try
        {
            var firstInstance = new TenantQuotaRepository(factory, NullLogger<TenantQuotaRepository>.Instance, tenantAccessor);
            (await firstInstance.GetOrCreateAsync(tenantId)).CurrentDailyTokens.Should().Be(0);
            await firstInstance.IncrementUsageAsync(tenantId, tokens: 17, costUsd: 0.25);
            await firstInstance.IncrementUsageAsync(tenantId, tokens: 9, costUsd: 0.05);

            var afterRestart = new TenantQuotaRepository(factory, NullLogger<TenantQuotaRepository>.Instance, tenantAccessor);
            var persisted = await afterRestart.GetOrCreateAsync(tenantId);
            persisted.CurrentDailyTokens.Should().Be(26);
            persisted.CurrentDailyCostUsd.Should().BeApproximately(0.30, 0.000001);
            persisted.CurrentDailyRequests.Should().Be(2);

            using (tenantAccessor.BeginScope(new TenantContext { TenantId = "quota-persist-other" }))
            {
                var otherTenant = await afterRestart.GetOrCreateAsync("quota-persist-other");
                otherTenant.CurrentDailyTokens.Should().Be(0);
                otherTenant.CurrentDailyCostUsd.Should().Be(0);
                otherTenant.CurrentDailyRequests.Should().Be(0);
            }
        }
        finally
        {
            await using var cleanup = factory.CreateDbContext();
            await cleanup.TenantQuotas.IgnoreQueryFilters().Where(quota => quota.TenantId == tenantId).ExecuteDeleteAsync();
        }
    }

    private sealed class QuotaDbContextFactory(DbContextOptions<AgenticDbContext> options, ITenantContextAccessor tenantAccessor)
        : IDbContextFactory<AgenticDbContext>
    {
        public AgenticDbContext CreateDbContext() => new(options, tenantAccessor);

        public Task<AgenticDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDbContext());
    }
}
