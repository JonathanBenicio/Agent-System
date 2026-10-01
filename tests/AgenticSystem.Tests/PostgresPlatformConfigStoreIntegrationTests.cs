#pragma warning disable MAAI001 // Validate the experimental MAF session-store contract used by the backend.

using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Core.Services;
using AgenticSystem.Infrastructure.AgentFramework;
using AgenticSystem.Infrastructure.Configuration;
using AgenticSystem.Infrastructure.Gateway;
using AgenticSystem.Infrastructure.LLM;
using AgenticSystem.Infrastructure.Persistence;
using AgenticSystem.Infrastructure.Persistence.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using Pgvector.EntityFrameworkCore;

namespace AgenticSystem.Tests;

public class PostgresPlatformConfigStoreIntegrationTests
{
    [RequiresPostgresAndOllamaFact]
    public async Task PlatformProviderChange_NotifiesTwoIndependentLlmManagersAndGatewayRegistries()
    {
        var connectionString = GetIsolatedConnectionString();
        var prefix = $"integration.multi-host.{Guid.NewGuid():N}";
        var providerPrefix = "llm.providers.ollama";
        var changedBy = $"validation-{Guid.NewGuid():N}";
        var tenantContext = new TenantContextAccessor();
        var options = new DbContextOptionsBuilder<AgenticDbContext>()
            .UseNpgsql(connectionString, postgres => postgres.UseVector())
            .Options;
        var factory = new FakeDbContextFactory
        {
            ContextCreator = () => new AgenticDbContext(options, tenantContext)
        };
        var tenantStore = new InMemoryTenantStore();
        await tenantStore.SaveAsync(new Tenant { Id = "provider-refresh-test-tenant", Name = "Provider refresh test" });
        var providerConfigKeys = new[] { $"{providerPrefix}.enabled", $"{providerPrefix}.model", "llm.default.provider" };
        var originalConfig = new Dictionary<string, PlatformConfigEntity?>();
        await using (var baseline = factory.CreateDbContext())
        {
            foreach (var key in providerConfigKeys)
            {
                var entity = await baseline.PlatformConfigs.AsNoTracking().SingleOrDefaultAsync(item => item.Key == key);
                originalConfig[key] = entity is null ? null : new PlatformConfigEntity
                {
                    Key = entity.Key,
                    Value = entity.Value,
                    EncryptedValue = entity.EncryptedValue,
                    IsSecret = entity.IsSecret,
                    ChangedBy = entity.ChangedBy,
                    UpdatedAt = entity.UpdatedAt
                };
            }
        }
        var encryption = Substitute.For<IConfigEncryptionService>();
        encryption.Encrypt(Arg.Any<string>()).Returns(call => $"cipher:{call.Arg<string>()}");
        encryption.Decrypt(Arg.Any<string>()).Returns(call => call.Arg<string>()[7..]);
        var host1Notifier = new ConfigReloadNotifier();
        var host2Notifier = new ConfigReloadNotifier();
        var platformStore = new PostgresPlatformConfigStore(
            factory,
            encryption,
            host1Notifier,
            NullLogger<PostgresPlatformConfigStore>.Instance,
            new SystemOperationContextAccessor());

        using (tenantContext.BeginScope(new TenantContext { TenantId = "platform-config-test" }))
        {
            await platformStore.SetValuesAsync(
            [
                new PlatformConfigValue($"{providerPrefix}.enabled", bool.FalseString),
                new PlatformConfigValue($"{providerPrefix}.model", "qwen2.5:0.5b"),
                new PlatformConfigValue("llm.default.provider", "Ollama")
            ],
            changedBy);
        }

        var host1 = CreateHost("host-one", host1Notifier);
        var host2 = CreateHost("host-two", host2Notifier);
        var host1Configuration = await host1.Manager.GetConfigurationAsync();
        var host2Configuration = await host2.Manager.GetConfigurationAsync();
        host1Configuration.Providers.Single(provider => provider.Name == "Ollama").IsEnabled.Should().BeFalse();
        host2Configuration.Providers.Single(provider => provider.Name == "Ollama").IsEnabled.Should().BeFalse();
        (await host1.Gateway.GetAllServicesStatusAsync()).Should().BeEmpty();
        (await host2.Gateway.GetAllServicesStatusAsync()).Should().BeEmpty();

        var changedAtHost1 = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var changedAtHost2 = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var host1Subscription = host1Notifier.OnChange(key =>
        {
            if (key == $"{providerPrefix}.enabled") changedAtHost1.TrySetResult(key);
        });
        using var host2Subscription = host2Notifier.OnChange(key =>
        {
            if (key == $"{providerPrefix}.enabled") changedAtHost2.TrySetResult(key);
        });

        var listenerConnection1 = CreateListenerConnectionString(connectionString, prefix + ".one");
        var listenerConnection2 = CreateListenerConnectionString(connectionString, prefix + ".two");
        using var listener1 = CreateListener(host1.Services, listenerConnection1);
        using var listener2 = CreateListener(host2.Services, listenerConnection2);
        await listener1.StartAsync(CancellationToken.None);
        await listener2.StartAsync(CancellationToken.None);

        try
        {
            await WaitForListenerAsync(connectionString, listenerConnection1);
            await WaitForListenerAsync(connectionString, listenerConnection2);
            using (tenantContext.BeginScope(new TenantContext { TenantId = "platform-config-test" }))
            {
                await platformStore.SetValuesAsync(
                [
                    new PlatformConfigValue($"{providerPrefix}.enabled", bool.TrueString),
                    new PlatformConfigValue($"{providerPrefix}.model", "qwen2.5:0.5b")
                ],
                changedBy);
            }

            (await changedAtHost1.Task.WaitAsync(TimeSpan.FromSeconds(10))).Should().Be($"{providerPrefix}.enabled");
            (await changedAtHost2.Task.WaitAsync(TimeSpan.FromSeconds(10))).Should().Be($"{providerPrefix}.enabled");
            var host1Updated = await host1.Manager.GetConfigurationAsync();
            var host2Updated = await host2.Manager.GetConfigurationAsync();
            host1Updated.Providers.Single(provider => provider.Name == "Ollama").IsEnabled.Should().BeTrue();
            host2Updated.Providers.Single(provider => provider.Name == "Ollama").IsEnabled.Should().BeTrue();
            (await host1.Gateway.GetServiceStatusAsync("Ollama")).IsEnabled.Should().BeTrue();
            (await host2.Gateway.GetServiceStatusAsync("Ollama")).IsEnabled.Should().BeTrue();

            var chatOptions = new ChatOptions { ModelId = "qwen2.5:0.5b", MaxOutputTokens = 64 };
            var request = new[] { new ChatMessage(ChatRole.User, "Responda em português com uma frase curta: o provider local está ativo?") };
            ChatResponse response1;
            using (tenantContext.BeginScope(new TenantContext { TenantId = "provider-refresh-test-tenant" }))
                response1 = await host1.ChatClient.GetResponseAsync(request, chatOptions);
            ChatResponse response2;
            using (tenantContext.BeginScope(new TenantContext { TenantId = "provider-refresh-test-tenant" }))
                response2 = await host2.ChatClient.GetResponseAsync(request, chatOptions);
            response1.Text.Should().NotBeNullOrWhiteSpace();
            response2.Text.Should().NotBeNullOrWhiteSpace();
            (await host1.Gateway.GetServiceStatusAsync("Ollama")).RequestCount.Should().Be(1);
            (await host2.Gateway.GetServiceStatusAsync("Ollama")).RequestCount.Should().Be(1);
        }
        finally
        {
            await listener1.StopAsync(CancellationToken.None);
            await listener2.StopAsync(CancellationToken.None);
            await using var cleanup = factory.CreateDbContext();
            foreach (var key in providerConfigKeys)
            {
                var current = await cleanup.PlatformConfigs.SingleOrDefaultAsync(item => item.Key == key);
                if (current is not null)
                    cleanup.PlatformConfigs.Remove(current);
                if (originalConfig[key] is { } original)
                    cleanup.PlatformConfigs.Add(original);
            }
            using (tenantContext.BeginScope(new TenantContext { TenantId = "platform-config-test" }))
                await cleanup.SaveChangesAsync();
            await cleanup.PlatformConfigAudits
                .Where(entry => entry.ChangedBy == changedBy && providerConfigKeys.Contains(entry.Key))
                .ExecuteDeleteAsync();
        }

        host1.Services.Dispose();
        host2.Services.Dispose();

        (ServiceProvider Services, LLMManager Manager, ServiceGateway Gateway, ContextAwareChatClient ChatClient) CreateHost(
            string hostName,
            IConfigReloadNotifier notifier)
        {
            var ollamaUrl = Environment.GetEnvironmentVariable("AGENTIC_TEST_OLLAMA_URL")!;
            var settings = Options.Create(new AgenticSystemSettings
            {
                Ollama = new OllamaSettings
                {
                    Enabled = true,
                    BaseUrl = ollamaUrl,
                    DefaultModel = "qwen2.5:0.5b",
                    Priority = 1
                }
            });
            var gateway = new ServiceGateway(new CostTracker(10m), NullLogger<ServiceGateway>.Instance);
            var registry = new GatewayProviderRegistry(gateway, settings);
            var services = new ServiceCollection()
                .AddHttpClient()
                .AddSingleton<IConfigReloadNotifier>(notifier)
                .AddSingleton<ITenantContextAccessor>(tenantContext)
                .AddSingleton<IPlatformConfigStore>(platformStore)
                .AddSingleton(registry)
                .BuildServiceProvider();
            var manager = new LLMManager(
                settings,
                NullLogger<LLMManager>.Instance,
                NullLoggerFactory.Instance,
                new LLMRuntimeContextAccessor(),
                tenantStore,
                new InMemorySessionStore(),
                services,
                notifier,
                registry,
                platformStore);
            var chatClient = new ContextAwareChatClient(
                manager,
                new LLMRuntimeContextAccessor(),
                Substitute.For<IQuotaEnforcer>(),
                Substitute.For<ITokenAuditService>(),
                NullLogger<ContextAwareChatClient>.Instance,
                gateway);
            _ = hostName;
            return (services, manager, gateway, chatClient);
        }

        string CreateListenerConnectionString(string connection, string name) =>
            new NpgsqlConnectionStringBuilder(connection) { ApplicationName = name }.ConnectionString;

        RealTimeConfigReloadBackgroundService CreateListener(IServiceProvider services, string listenerConnection)
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:SessionStore"] = listenerConnection
            }).Build();
            return new RealTimeConfigReloadBackgroundService(
                services,
                config,
                NullLogger<RealTimeConfigReloadBackgroundService>.Instance);
        }
    }

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
        var sessionStore = new PostgresSessionStore(factory, NullLogger<PostgresSessionStore>.Instance, tenantContext);
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
            Substitute.For<ILogger<PostgresPlatformConfigStore>>(),
            new SystemOperationContextAccessor());
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

public sealed class RequiresPostgresAndOllamaFactAttribute : FactAttribute
{
    public RequiresPostgresAndOllamaFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AGENTIC_TEST_POSTGRES"))
            || string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AGENTIC_EF_CONNECTION")))
        {
            Skip = "Set both PostgreSQL connection variables to the isolated backend-validation Compose database.";
        }
        else if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AGENTIC_TEST_OLLAMA_URL")))
        {
            Skip = "Set AGENTIC_TEST_OLLAMA_URL to the isolated backend-validation Ollama endpoint.";
        }
    }
}
