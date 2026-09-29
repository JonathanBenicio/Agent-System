using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Core.Services;
using AgenticSystem.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;

namespace AgenticSystem.Tests;

public sealed class PostgresChatSettingsStoreTests
{
    [RequiresPostgresFact]
    public async Task PreferencesPersistByTenantAndUserAndAreNotVisibleAcrossEitherBoundary()
    {
        var connection = GetIsolatedConnectionString();
        var tenant = new TenantContextAccessor();
        var options = new DbContextOptionsBuilder<AgenticDbContext>()
            .UseNpgsql(connection, postgres => postgres.UseVector()).Options;
        var factory = new FakeDbContextFactory { ContextCreator = () => new AgenticDbContext(options, tenant) };
        var store = new PostgresChatSettingsStore(factory);
        var settings = new ChatSettings("chat-settings-123-tenant", "chat-settings-123-user",
            "OpenAI", "validation-model", DateTime.UtcNow);
        try
        {
            using (tenant.BeginScope(new TenantContext { TenantId = settings.TenantId }))
                await store.SaveAsync(settings);

            using (tenant.BeginScope(new TenantContext { TenantId = "chat-settings-123-other-tenant" }))
                (await store.GetAsync(settings.TenantId, settings.UserId)).Should().BeNull();

            using (tenant.BeginScope(new TenantContext { TenantId = settings.TenantId }))
            {
                (await store.GetAsync(settings.TenantId, "chat-settings-123-other-user")).Should().BeNull();
                var restored = await store.GetAsync(settings.TenantId, settings.UserId);
                restored.Should().NotBeNull();
                restored!.TenantId.Should().Be(settings.TenantId);
                restored.UserId.Should().Be(settings.UserId);
                restored.Provider.Should().Be(settings.Provider);
                restored.Model.Should().Be(settings.Model);
                restored.UpdatedAt.Should().BeCloseTo(settings.UpdatedAt, TimeSpan.FromMicroseconds(2));
            }
        }
        finally
        {
            using var scope = tenant.BeginScope(new TenantContext { TenantId = settings.TenantId });
            await using var db = factory.CreateDbContext();
            var row = await db.ChatSettings.FirstOrDefaultAsync(item => item.UserId == settings.UserId);
            if (row is not null)
            {
                db.ChatSettings.Remove(row);
                await db.SaveChangesAsync();
            }
        }
    }

    private static string GetIsolatedConnectionString()
    {
        var connection = Environment.GetEnvironmentVariable("AGENTIC_TEST_POSTGRES");
        ArgumentException.ThrowIfNullOrWhiteSpace(connection);
        var target = new Npgsql.NpgsqlConnectionStringBuilder(connection);
        var efConnection = Environment.GetEnvironmentVariable("AGENTIC_EF_CONNECTION");
        ArgumentException.ThrowIfNullOrWhiteSpace(efConnection);
        var efTarget = new Npgsql.NpgsqlConnectionStringBuilder(efConnection);
        if (target.Host != "127.0.0.1" || target.Port != 55432 || target.Database != "backend_validation"
            || target.Username != "validation" || efTarget.Host != target.Host || efTarget.Port != target.Port
            || efTarget.Database != target.Database || efTarget.Username != target.Username || efTarget.Password != target.Password)
            throw new InvalidOperationException("PostgreSQL validation is restricted to the isolated backend-validation Compose database.");
        return connection;
    }
}
