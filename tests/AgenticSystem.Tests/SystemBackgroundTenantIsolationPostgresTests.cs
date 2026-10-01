using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Core.Services;
using AgenticSystem.Infrastructure.Persistence;
using AgenticSystem.Infrastructure.Persistence.Entities;
using AgenticSystem.Infrastructure.Services;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Pgvector.EntityFrameworkCore;

namespace AgenticSystem.Tests;

public sealed class SystemBackgroundTenantIsolationPostgresTests
{
    [RequiresPostgresFact]
    public async Task OutboxProcessor_DispatchesEachMessageAndMarksItUnderItsSourceTenant()
    {
        var accessor = new TenantContextAccessor();
        var options = CreateOptions(GetIsolatedConnectionString());
        var factory = CreateFactory(options, accessor);
        await using (var setup = factory.CreateDbContext())
            await setup.Database.MigrateAsync();

        var tenantA = $"outbox-pg-a-{Guid.NewGuid():N}";
        var tenantB = $"outbox-pg-b-{Guid.NewGuid():N}";
        var ids = new Dictionary<string, Guid>();
        foreach (var tenantId in new[] { tenantA, tenantB })
        {
            using var tenantScope = accessor.BeginScope(new TenantContext { TenantId = tenantId });
            await using var db = factory.CreateDbContext();
            var messageId = Guid.NewGuid();
            ids[tenantId] = messageId;
            db.OutboxMessages.Add(new OutboxMessageEntity
            {
                Id = messageId,
                TenantId = tenantId,
                EventType = typeof(TenantScopedOutboxNotification).AssemblyQualifiedName!,
                PayloadJson = System.Text.Json.JsonSerializer.Serialize(new TenantScopedOutboxNotification(tenantId)),
                CreatedAt = DateTime.UtcNow.AddSeconds(tenantId == tenantA ? -2 : -1)
            });
            await db.SaveChangesAsync();
        }

        var dispatches = new List<(string TenantContext, string EventTenant)>();
        var publisher = Substitute.For<IPublisher>();
        publisher.Publish(Arg.Any<INotification>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var notification = call.Arg<INotification>().Should().BeOfType<TenantScopedOutboxNotification>().Subject;
                dispatches.Add((accessor.CurrentTenantId, notification.TenantId));
                return Task.CompletedTask;
            });

        var services = new ServiceCollection();
        services.AddScoped<AgenticDbContext>(_ => factory.CreateDbContext());
        services.AddSingleton<IPublisher>(publisher);
        using var provider = services.BuildServiceProvider();
        var processor = new OutboxProcessorBackgroundService(
            provider,
            NullLogger<OutboxProcessorBackgroundService>.Instance,
            accessor,
            new SystemOperationContextAccessor());

        try
        {
            var method = typeof(OutboxProcessorBackgroundService).GetMethod(
                "ProcessOutboxMessagesAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            await (Task)method.Invoke(processor, [CancellationToken.None])!;

            dispatches.Should().Equal((tenantA, tenantA), (tenantB, tenantB));
            foreach (var tenantId in new[] { tenantA, tenantB })
            {
                using var tenantScope = accessor.BeginScope(new TenantContext { TenantId = tenantId });
                await using var db = factory.CreateDbContext();
                var saved = await db.OutboxMessages.SingleAsync(message => message.Id == ids[tenantId]);
                saved.ProcessedAt.Should().NotBeNull();
                saved.Error.Should().BeNull();
            }
        }
        finally
        {
            foreach (var tenantId in new[] { tenantA, tenantB })
            {
                using var tenantScope = accessor.BeginScope(new TenantContext { TenantId = tenantId });
                await using var db = factory.CreateDbContext();
                var message = await db.OutboxMessages.SingleOrDefaultAsync(item => item.Id == ids[tenantId]);
                if (message is not null)
                {
                    db.OutboxMessages.Remove(message);
                    await db.SaveChangesAsync();
                }
            }
        }
    }

    [RequiresPostgresFact]
    public async Task SecretRotation_EnumeratesTenantsAndAuditsOnlyTheirExpiredKeys()
    {
        var accessor = new TenantContextAccessor();
        var options = CreateOptions(GetIsolatedConnectionString());
        var factory = CreateFactory(options, accessor);
        await using (var setup = factory.CreateDbContext())
            await setup.Database.MigrateAsync();

        var tenantA = $"rotation-pg-a-{Guid.NewGuid():N}";
        var tenantB = $"rotation-pg-b-{Guid.NewGuid():N}";
        var keyA = $"provider.{tenantA}.apiKey";
        var keyB = $"provider.{tenantB}.apiKey";
        var eventBus = Substitute.For<IEventBus>();
        var configStore = new PostgresConfigStore(factory, NullLogger<PostgresConfigStore>.Instance, eventBus);

        foreach (var (tenantId, key) in new[] { (tenantA, keyA), (tenantB, keyB) })
        {
            using var tenantScope = accessor.BeginScope(new TenantContext { TenantId = tenantId });
            await configStore.SaveAsync(new ConfigEntry
            {
                Id = Guid.NewGuid().ToString("N"),
                Key = key,
                Value = "secret-value-must-not-be-audited",
                IsSecret = true,
                Category = ConfigCategory.Credentials,
                Status = ConfigEntryStatus.Active,
                Provider = "TestProvider",
                ExpiresAt = DateTime.UtcNow.AddDays(1)
            });
        }

        var tenantStore = Substitute.For<ITenantStore>();
        tenantStore.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns([new Tenant { Id = tenantA, Name = tenantA }, new Tenant { Id = tenantB, Name = tenantB }]);
        var configManager = new ConfigManager(
            configStore,
            Substitute.For<IConfigEncryptionService>(),
            Substitute.For<IConfigReloadNotifier>(),
            NullLogger<ConfigManager>.Instance);
        var audited = new List<(string TenantId, string? Description)>();
        var auditLog = Substitute.For<IAuditLog>();
        auditLog.RecordAsync(Arg.Any<AuditEntry>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                audited.Add((accessor.CurrentTenantId, call.Arg<AuditEntry>().Description));
                return Task.CompletedTask;
            });

        var services = new ServiceCollection();
        services.AddSingleton<ITenantContextAccessor>(accessor);
        services.AddSingleton<ISystemOperationContextAccessor>(new SystemOperationContextAccessor());
        services.AddSingleton<ITenantStore>(tenantStore);
        services.AddSingleton<IConfigManager>(configManager);
        services.AddSingleton<IAuditLog>(auditLog);
        using var provider = services.BuildServiceProvider();
        var service = new SecretRotationBackgroundService(provider, NullLogger<SecretRotationBackgroundService>.Instance);

        try
        {
            var method = typeof(SecretRotationBackgroundService).GetMethod(
                "CheckExpiredSecretsAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            await (Task)method.Invoke(service, [CancellationToken.None])!;

            audited.Select(item => item.TenantId).Should().Equal(tenantA, tenantB);
            audited.Select(item => item.Description).Should().Contain(description => description != null && description.Contains(keyA, StringComparison.Ordinal));
            audited.Select(item => item.Description).Should().Contain(description => description != null && description.Contains(keyB, StringComparison.Ordinal));
            audited.Select(item => item.Description).Should().OnlyContain(description =>
                description != null && !description.Contains("secret-value-must-not-be-audited", StringComparison.Ordinal));
        }
        finally
        {
            foreach (var (tenantId, key) in new[] { (tenantA, keyA), (tenantB, keyB) })
            {
                using var tenantScope = accessor.BeginScope(new TenantContext { TenantId = tenantId });
                await configStore.DeleteAsync(key);
            }
        }
    }

    private static DbContextOptions<AgenticDbContext> CreateOptions(string connectionString) =>
        new DbContextOptionsBuilder<AgenticDbContext>()
            .UseNpgsql(connectionString, npgsql =>
            {
                npgsql.UseVector();
                npgsql.MigrationsHistoryTable("__ef_migrations_history");
            })
            .Options;

    private static FakeDbContextFactory CreateFactory(
        DbContextOptions<AgenticDbContext> options,
        ITenantContextAccessor accessor) => new()
    {
        ContextCreator = () => new AgenticDbContext(options, accessor)
    };

    private static string GetIsolatedConnectionString()
    {
        var connectionString = Environment.GetEnvironmentVariable("AGENTIC_TEST_POSTGRES");
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        var target = new NpgsqlConnectionStringBuilder(connectionString);
        if (target.Host != "127.0.0.1" || target.Port != 55432 ||
            target.Database != "backend_validation" || target.Username != "validation")
            throw new InvalidOperationException("System background isolation tests only accept the isolated backend-validation Compose database.");

        return connectionString;
    }
}
