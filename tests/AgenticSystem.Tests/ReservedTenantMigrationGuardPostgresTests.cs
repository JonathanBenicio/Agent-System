using AgenticSystem.Core.Services;
using AgenticSystem.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pgvector.EntityFrameworkCore;

namespace AgenticSystem.Tests;

public sealed class ReservedTenantMigrationGuardPostgresTests
{
    [ReviewSessionPostgresFact]
    public async Task SeparatePlatformResources_FailsClosedAndPreservesReservedTenantRows()
    {
        var configured = Environment.GetEnvironmentVariable("AGENTIC_REVIEW_POSTGRES");
        ArgumentException.ThrowIfNullOrWhiteSpace(configured);
        var source = new NpgsqlConnectionStringBuilder(configured);
        if (source.Host != "127.0.0.1" || source.Port != 55432 || source.Username != "validation" ||
            !(source.Database == "backend_validation" || source.Database!.StartsWith("review_pr152_", StringComparison.Ordinal)))
            throw new InvalidOperationException("Requires the isolated Agent-System Compose PostgreSQL on port 55432.");
        source.IncludeErrorDetail = true;

        var databaseName = $"review_pr152_tenant_guard_{Guid.NewGuid():N}";
        var adminConnection = new NpgsqlConnectionStringBuilder(source.ConnectionString) { Database = "postgres" };

        try
        {
            await using (var admin = new NpgsqlConnection(adminConnection.ConnectionString))
            {
                await admin.OpenAsync();
                await using var create = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", admin);
                await create.ExecuteNonQueryAsync();
            }

            var target = new NpgsqlConnectionStringBuilder(source.ConnectionString) { Database = databaseName };
            var options = new DbContextOptionsBuilder<AgenticDbContext>()
                .UseNpgsql(target.ConnectionString, postgres =>
                {
                    postgres.UseVector();
                    postgres.MigrationsHistoryTable("__ef_migrations_history");
                })
                .Options;

            await using (var db = new AgenticDbContext(options, new TenantContextAccessor()))
            {
                await db.Database.MigrateAsync("20260930050717_AddFidesTenantPolicies");
                await using var seed = new NpgsqlConnection(target.ConnectionString);
                await seed.OpenAsync();
                await using var insert = new NpgsqlCommand("""
                    INSERT INTO tenants (id, name, slug, plan, limits, is_active, created_at, provider_api_keys, settings)
                    VALUES ('default', 'Legacy default', 'legacy-default', 'free', '{}'::jsonb, true, now(), '[]'::jsonb, '{}'::jsonb);
                    """, seed);
                await insert.ExecuteNonQueryAsync();

                var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.MigrateAsync());
                error.MessageText.Should().Contain("reserved tenant IDs default/system-devui");
                error.Detail.Should().Contain("tenants.id: 1 row(s)");
                error.Hint.Should().Contain("Map each row to its approved real tenant or archive it");
            }

            await using (var verify = new NpgsqlConnection(target.ConnectionString))
            {
                await verify.OpenAsync();
                await using var count = new NpgsqlCommand("SELECT count(*) FROM tenants WHERE id = 'default'", verify);
                ((long)(await count.ExecuteScalarAsync())!).Should().Be(1);
            }
        }
        finally
        {
            await using var admin = new NpgsqlConnection(adminConnection.ConnectionString);
            await admin.OpenAsync();
            await using (var terminate = new NpgsqlCommand("""
                SELECT pg_terminate_backend(pid) FROM pg_stat_activity
                WHERE datname = @database AND pid <> pg_backend_pid();
                """, admin))
            {
                terminate.Parameters.AddWithValue("database", databaseName);
                await terminate.ExecuteNonQueryAsync();
            }
            await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{databaseName}\"", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }
}
