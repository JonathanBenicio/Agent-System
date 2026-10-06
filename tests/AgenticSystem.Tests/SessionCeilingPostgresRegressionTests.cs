using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Core.Services;
using AgenticSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Pgvector.EntityFrameworkCore;

namespace AgenticSystem.Tests;

public sealed class SessionCeilingPostgresRegressionTests
{
    [ReviewSessionPostgresFact]
    public async Task ConcurrentCreationAcrossStoreInstancesKeepsTenantCeiling()
    {
        var target = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("AGENTIC_REVIEW_POSTGRES"));
        if (target.Host != "127.0.0.1" || target.Port != 55432 || target.Username != "validation" ||
            !target.Database!.StartsWith("review_pr152_", StringComparison.Ordinal))
            throw new InvalidOperationException("Review test requires its exclusive Compose database on 55432.");
        var accessor = new TenantContextAccessor();
        var options = new DbContextOptionsBuilder<AgenticDbContext>()
            .UseNpgsql(target.ConnectionString, postgres =>
            {
                postgres.UseVector();
                postgres.MigrationsHistoryTable("__ef_migrations_history");
            }).Options;
        var tenantId = $"session-ceiling-{Guid.NewGuid():N}";
        await using (var setup = new AgenticDbContext(options, accessor))
        {
            await setup.Database.MigrateAsync();
            setup.Tenants.Add(new Tenant { Id = tenantId, Name = tenantId, Slug = tenantId });
            await setup.SaveChangesAsync();
        }
        try
        {
            using var tenantScope = accessor.BeginScope(new TenantContext { TenantId = tenantId });
            var factory = new Factory(options, accessor);
            var results = await Task.WhenAll(Enumerable.Range(0, 12).Select(async i =>
            {
                var store = new PostgresSessionStore(factory, NullLogger<PostgresSessionStore>.Instance, accessor);
                return await store.TryCreateAsync(new SessionData
                {
                    Id = $"{tenantId}-{i}", UserId = $"user-{i}", TenantId = tenantId, StartedAt = DateTime.UtcNow
                }, 3);
            }));
            results.Count(created => created).Should().Be(3);
            var verify = new PostgresSessionStore(factory, NullLogger<PostgresSessionStore>.Instance, accessor);
            (await verify.CountActiveAsync(tenantId)).Should().Be(3);
            await using var db = new AgenticDbContext(options, accessor);
            db.SessionRecords.AddRange(Enumerable.Range(0, 30).Select(i => new AgenticSystem.Infrastructure.Persistence.Entities.SessionRecordEntity
            {
                Id = $"{tenantId}-ended-{i}", TenantId = tenantId, UserId = "history",
                StartedAt = DateTime.UtcNow.AddMinutes(1), EndedAt = DateTime.UtcNow, DataJson = "{}"
            }));
            await db.SaveChangesAsync();
            (await verify.CountActiveAsync(tenantId)).Should().Be(3);
        }
        finally
        {
            using var tenantScope = accessor.BeginScope(new TenantContext { TenantId = tenantId });
            await using var cleanup = new AgenticDbContext(options, accessor);
            await cleanup.SessionRecords.Where(session => session.TenantId == tenantId).ExecuteDeleteAsync();
            await cleanup.Tenants.Where(tenant => tenant.Id == tenantId).ExecuteDeleteAsync();
        }
    }

    private sealed class Factory(DbContextOptions<AgenticDbContext> options, ITenantContextAccessor tenant)
        : IDbContextFactory<AgenticDbContext>
    {
        public AgenticDbContext CreateDbContext() => new(options, tenant);
    }
}

public sealed class ReviewSessionPostgresFactAttribute : FactAttribute
{
    public ReviewSessionPostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AGENTIC_REVIEW_POSTGRES")))
            Skip = "Requires exclusive review_pr152_* Compose database on 55432.";
    }
}
