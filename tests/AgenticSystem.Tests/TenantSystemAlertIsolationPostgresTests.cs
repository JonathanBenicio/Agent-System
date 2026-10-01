using AgenticSystem.Core.Models;
using AgenticSystem.Core.Services;
using AgenticSystem.Infrastructure.Persistence;
using AgenticSystem.Infrastructure.Persistence.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pgvector.EntityFrameworkCore;

namespace AgenticSystem.Tests;

public sealed class TenantSystemAlertIsolationPostgresTests
{
    [RequiresPostgresFact]
    public async Task TenantQuotaAlerts_AreVisibleOnlyInsideTheirOwningTenant()
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

        var tenantA = $"alert-a-{Guid.NewGuid():N}";
        var tenantB = $"alert-b-{Guid.NewGuid():N}";
        var alertA = Guid.NewGuid().ToString("N");
        var alertB = Guid.NewGuid().ToString("N");

        using (accessor.BeginScope(new TenantContext { TenantId = tenantA }))
        {
            await using var db = new AgenticDbContext(options, accessor);
            db.TenantSystemAlerts.Add(new TenantSystemAlertEntity
            {
                Id = alertA,
                TenantId = tenantA,
                Type = "Requests",
                Severity = "Critical",
                Message = "Tenant A quota alert",
                ProviderName = "TestProvider"
            });
            await db.SaveChangesAsync();
        }

        using (accessor.BeginScope(new TenantContext { TenantId = tenantB }))
        {
            await using var db = new AgenticDbContext(options, accessor);
            db.TenantSystemAlerts.Add(new TenantSystemAlertEntity
            {
                Id = alertB,
                TenantId = tenantB,
                Type = "Requests",
                Severity = "Critical",
                Message = "Tenant B quota alert",
                ProviderName = "TestProvider"
            });
            await db.SaveChangesAsync();
        }

        using (accessor.BeginScope(new TenantContext { TenantId = tenantA }))
        {
            await using var db = new AgenticDbContext(options, accessor);
            (await db.TenantSystemAlerts.Select(alert => alert.Id).ToListAsync()).Should().Equal(alertA);
        }

        using (accessor.BeginScope(new TenantContext { TenantId = tenantB }))
        {
            await using var db = new AgenticDbContext(options, accessor);
            (await db.TenantSystemAlerts.Select(alert => alert.Id).ToListAsync()).Should().Equal(alertB);
        }

        await using var cleanup = new AgenticDbContext(options, accessor);
        await cleanup.TenantSystemAlerts.IgnoreQueryFilters()
            .Where(alert => alert.Id == alertA || alert.Id == alertB)
            .ExecuteDeleteAsync();
    }

    private static string GetIsolatedConnectionString()
    {
        var connectionString = Environment.GetEnvironmentVariable("AGENTIC_TEST_POSTGRES");
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        var target = new NpgsqlConnectionStringBuilder(connectionString);
        if (target.Host != "127.0.0.1" || target.Port != 55432 ||
            target.Database != "backend_validation" || target.Username != "validation")
            throw new InvalidOperationException("Tenant alert isolation test only accepts the isolated backend-validation Compose database.");

        return connectionString;
    }
}
