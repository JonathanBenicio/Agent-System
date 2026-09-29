#pragma warning disable MAAI001 // Validate the experimental MAF session-store contract used by the backend.

using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Core.Services;
using AgenticSystem.Infrastructure.AgentFramework;
using AgenticSystem.Infrastructure.Persistence;
using AgenticSystem.Infrastructure.Persistence.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Agents.AI;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Pgvector.EntityFrameworkCore;

namespace AgenticSystem.Tests;

public class PostgresPlatformConfigStoreIntegrationTests
{
    [RequiresPostgresFact]
    public async Task MafSessionSnapshotSurvivesAdapterRestartAndRejectsDifferentTenantOwner()
    {
        var connectionString = GetIsolatedConnectionString();
        var sessionId = $"integration.maf-session.{Guid.NewGuid():N}";
        var tenantContext = new TenantContextAccessor();
        var options = new DbContextOptionsBuilder<AgenticDbContext>()
            .UseNpgsql(connectionString, postgres => postgres.UseVector())
            .Options;
        var factory = new FakeDbContextFactory
        {
            ContextCreator = () => new AgenticDbContext(options, tenantContext)
        };
        var logger = NullLogger<SimpleSessionStoreAdapter>.Instance;
        var sessionStore = new PostgresSessionStore(factory, NullLogger<PostgresSessionStore>.Instance);
        var session = new SessionData
        {
            Id = sessionId,
            TenantId = "tenant-a",
            UserId = "user-1"
        };
        var agent = CreateTestAgent("Orchestrator");
        var key = new AgentSessionStoreKey(sessionId)
            .WithPartition("isolation", "tenant-a:user-1");

        try
        {
            using (tenantContext.BeginScope(new TenantContext { TenantId = "tenant-a" }))
            {
                await sessionStore.SaveAsync(session);
                var firstAdapter = new SimpleSessionStoreAdapter(sessionStore, logger);
                await firstAdapter.SaveSessionAsync(agent, key, Substitute.For<AgentSession>());

                // A new adapter and agent instance model a process restart; only PostgreSQL state survives.
                var restoredSession = Substitute.For<AgentSession>();
                var restartedAgent = CreateTestAgent("Orchestrator", restoredSession);
                var restartedAdapter = new SimpleSessionStoreAdapter(sessionStore, logger);
                var loaded = await restartedAdapter.GetSessionAsync(restartedAgent, key);

                loaded.Should().BeSameAs(restoredSession);
                await using var verifyDb = factory.CreateDbContext();
                var persisted = await verifyDb.SessionRecords.SingleAsync(record => record.Id == sessionId);
                persisted.DataJson.Should().Contain("frameworkSessionState:orchestrator:scope:");
                persisted.DataJson.Should().Contain("persisted-across-restart");
            }

            using (tenantContext.BeginScope(new TenantContext { TenantId = "tenant-b" }))
            {
                var foreignKey = new AgentSessionStoreKey(sessionId)
                    .WithPartition("isolation", "tenant-b:user-1");
                var restartedAgent = CreateTestAgent("Orchestrator", Substitute.For<AgentSession>());
                var restartedAdapter = new SimpleSessionStoreAdapter(sessionStore, logger);
                var act = () => restartedAdapter.GetSessionAsync(restartedAgent, foreignKey).AsTask();
                (await act()).Should().BeNull();
            }
        }
        finally
        {
            using (tenantContext.BeginScope(new TenantContext { TenantId = "tenant-a" }))
            {
                await using var cleanup = factory.CreateDbContext();
                await cleanup.SessionRecords.Where(record => record.Id == sessionId).ExecuteDeleteAsync();
            }
        }
    }

    [RequiresPostgresFact]
    public async Task GlobalProviderSettingsSurviveTenantSwitchAndAreEncryptedAndAudited()
    {
        var connectionString = Environment.GetEnvironmentVariable("AGENTIC_TEST_POSTGRES");
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        var efConnectionString = Environment.GetEnvironmentVariable("AGENTIC_EF_CONNECTION");
        ArgumentException.ThrowIfNullOrWhiteSpace(efConnectionString);
        var testTarget = new NpgsqlConnectionStringBuilder(connectionString);
        var efTarget = new NpgsqlConnectionStringBuilder(efConnectionString);
        if (testTarget.Host != "127.0.0.1"
            || testTarget.Port != 55432
            || testTarget.Database != "backend_validation"
            || testTarget.Username != "validation"
            || efTarget.Host != testTarget.Host
            || efTarget.Port != testTarget.Port
            || efTarget.Database != testTarget.Database
            || efTarget.Username != testTarget.Username
            || efTarget.Password != testTarget.Password)
        {
            throw new InvalidOperationException(
                "PostgreSQL integration tests are restricted to tests/backend-validation/compose.yml (127.0.0.1:55432/backend_validation). Set both connection variables to that isolated database.");
        }

        var prefix = $"integration.platform.{Guid.NewGuid():N}";
        var tenantContext = new TenantContextAccessor();
        var options = new DbContextOptionsBuilder<AgenticDbContext>()
            .UseNpgsql(connectionString, postgres => postgres.UseVector())
            .Options;
        var factory = new FakeDbContextFactory
        {
            ContextCreator = () => new AgenticDbContext(options, tenantContext)
        };
        var encryption = Substitute.For<IConfigEncryptionService>();
        encryption.Encrypt(Arg.Any<string>()).Returns(call => $"cipher:{call.Arg<string>()}");
        encryption.Decrypt(Arg.Any<string>()).Returns(call => call.Arg<string>()[7..]);
        var reloadNotifier = Substitute.For<IConfigReloadNotifier>();
        var store = new PostgresPlatformConfigStore(
            factory,
            encryption,
            reloadNotifier,
            Substitute.For<ILogger<PostgresPlatformConfigStore>>());
        var notification = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var notifier = new ConfigReloadNotifier();
        using var subscription = notifier.OnChange(key =>
        {
            if (key.StartsWith(prefix, StringComparison.Ordinal))
                notification.TrySetResult(key);
        });
        var listenerConnectionString = new NpgsqlConnectionStringBuilder(connectionString)
        {
            ApplicationName = $"backend-validation-{Guid.NewGuid():N}"
        }.ConnectionString;
        var listenerConfiguration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:SessionStore"] = listenerConnectionString
            })
            .Build();
        using var listenerServices = new ServiceCollection()
            .AddSingleton<IConfigReloadNotifier>(notifier)
            .BuildServiceProvider();
        using var listenerService = new RealTimeConfigReloadBackgroundService(
            listenerServices,
            listenerConfiguration,
            NullLogger<RealTimeConfigReloadBackgroundService>.Instance);
        await listenerService.StartAsync(CancellationToken.None);

        try
        {
            await WaitForListenerAsync(connectionString, listenerConnectionString);

            using (tenantContext.BeginScope(new TenantContext { TenantId = "tenant-a" }))
            {
                await store.SetValuesAsync(
                [
                    new PlatformConfigValue($"{prefix}.apiKey", "platform-key", IsSecret: true),
                    new PlatformConfigValue($"{prefix}.enabled", bool.TrueString)
                ],
                "platform-admin");
            }

            var changedKey = await notification.Task.WaitAsync(TimeSpan.FromSeconds(5));
            changedKey.Should().BeOneOf($"{prefix}.apiKey", $"{prefix}.enabled");

            using (tenantContext.BeginScope(new TenantContext { TenantId = "tenant-b" }))
            {
                (await store.GetValueAsync($"{prefix}.apiKey")).Should().Be("platform-key");
                (await store.GetValueAsync($"{prefix}.enabled")).Should().Be(bool.TrueString);
                await using var db = factory.CreateDbContext();
                var secret = await db.PlatformConfigs.SingleAsync(setting => setting.Key == $"{prefix}.apiKey");
                secret.Value.Should().Be("********");
                secret.EncryptedValue.Should().Be("cipher:platform-key");

                var audit = await db.PlatformConfigAudits.SingleAsync(entry => entry.Key == $"{prefix}.apiKey");
                audit.ChangedBy.Should().Be("platform-admin");
                audit.NewValueHash.Should().NotBe("platform-key");
            }
        }
        finally
        {
            await listenerService.StopAsync(CancellationToken.None);
            await using var cleanup = factory.CreateDbContext();
            await cleanup.PlatformConfigAudits.Where(entry => entry.Key.StartsWith(prefix)).ExecuteDeleteAsync();
            await cleanup.PlatformConfigs.Where(setting => setting.Key.StartsWith(prefix)).ExecuteDeleteAsync();
        }
    }

    private static async Task WaitForListenerAsync(string connectionString, string listenerConnectionString)
    {
        var applicationName = new NpgsqlConnectionStringBuilder(listenerConnectionString).ApplicationName
            ?? throw new InvalidOperationException("The PostgreSQL listener application name was not configured.");
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        while (!timeout.IsCancellationRequested)
        {
            await using var command = new NpgsqlCommand(
                "SELECT EXISTS (SELECT 1 FROM pg_stat_activity WHERE application_name = @app AND query LIKE 'LISTEN config_changed%')",
                connection);
            command.Parameters.AddWithValue("app", applicationName);
            if (await command.ExecuteScalarAsync(timeout.Token) is true)
                return;

            await Task.Delay(TimeSpan.FromMilliseconds(100), timeout.Token);
        }

        throw new TimeoutException("The PostgreSQL configuration listener did not subscribe to config_changed.");
    }

    private static string GetIsolatedConnectionString()
    {
        var connectionString = Environment.GetEnvironmentVariable("AGENTIC_TEST_POSTGRES");
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        var target = new NpgsqlConnectionStringBuilder(connectionString);
        if (target.Host != "127.0.0.1"
            || target.Port != 55432
            || target.Database != "backend_validation"
            || target.Username != "validation")
        {
            throw new InvalidOperationException(
                "PostgreSQL integration tests are restricted to tests/backend-validation/compose.yml (127.0.0.1:55432/backend_validation). Set both connection variables to that isolated database.");
        }

        var efConnectionString = Environment.GetEnvironmentVariable("AGENTIC_EF_CONNECTION");
        ArgumentException.ThrowIfNullOrWhiteSpace(efConnectionString);
        var efTarget = new NpgsqlConnectionStringBuilder(efConnectionString);
        if (efTarget.Host != target.Host
            || efTarget.Port != target.Port
            || efTarget.Database != target.Database
            || efTarget.Username != target.Username
            || efTarget.Password != target.Password)
        {
            throw new InvalidOperationException("AGENTIC_TEST_POSTGRES and AGENTIC_EF_CONNECTION must point to the same isolated Compose database.");
        }

        return connectionString;
    }

    private static TestAgent CreateTestAgent(string name, AgentSession? restoredSession = null)
    {
        var agent = Substitute.For<TestAgent>();
        agent.Name.Returns(name);
        agent.SerializeSessionAsync(
                Arg.Any<AgentSession>(),
                Arg.Any<JsonSerializerOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(ParseSessionJson()));
        agent.DeserializeSessionAsync(
                Arg.Any<JsonElement>(),
                Arg.Any<JsonSerializerOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(restoredSession ?? Substitute.For<AgentSession>()));
        return agent;
    }

    private static JsonElement ParseSessionJson()
    {
        using var document = JsonDocument.Parse("{\"messages\":[\"persisted-across-restart\"]}");
        return document.RootElement.Clone();
    }

    public abstract class TestAgent : AIAgent { }
}

public sealed class RequiresPostgresFactAttribute : FactAttribute
{
    public RequiresPostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AGENTIC_TEST_POSTGRES"))
            || string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AGENTIC_EF_CONNECTION")))
            Skip = "Set both PostgreSQL connection variables to the isolated backend-validation Compose database.";
    }
}
