using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Core.Services;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace AgenticSystem.Tests;

public sealed class SecretRotationTenantIsolationTests
{
    [Fact]
    public async Task CheckExpiredSecrets_EnumeratesAndAuditsEachRealTenantSeparately()
    {
        var tenantA = $"rotation-a-{Guid.NewGuid():N}";
        var tenantB = $"rotation-b-{Guid.NewGuid():N}";
        var tenantAccessor = new TenantContextAccessor();
        var tenantStore = Substitute.For<ITenantStore>();
        tenantStore.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns([new Tenant { Id = tenantA, Name = tenantA }, new Tenant { Id = tenantB, Name = tenantB }]);

        var configManager = Substitute.For<IConfigManager>();
        configManager.GetExpiredSecretsAsync(Arg.Any<TimeSpan?>())
            .Returns(_ =>
            {
                var activeTenant = tenantAccessor.CurrentTenantId;
                return Task.FromResult<IEnumerable<ConfigEntry>>
                ([
                    new ConfigEntry
                    {
                        Key = $"provider.{activeTenant}.apiKey",
                        Value = "secret-value-must-not-be-audited",
                        IsSecret = true,
                        Category = ConfigCategory.Credentials,
                        Provider = "TestProvider",
                        ExpiresAt = DateTime.UtcNow.AddDays(1)
                    }
                ]);
            });

        var auditedUnder = new List<string>();
        var auditEntries = new List<AuditEntry>();
        var auditLog = Substitute.For<IAuditLog>();
        auditLog.RecordAsync(Arg.Any<AuditEntry>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                auditedUnder.Add(tenantAccessor.CurrentTenantId);
                auditEntries.Add(call.Arg<AuditEntry>());
                return Task.CompletedTask;
            });

        var services = new ServiceCollection();
        services.AddSingleton<ITenantContextAccessor>(tenantAccessor);
        services.AddSingleton(tenantStore);
        services.AddSingleton(configManager);
        services.AddSingleton(auditLog);
        services.AddSingleton<ISystemOperationContextAccessor>(new SystemOperationContextAccessor());
        using var provider = services.BuildServiceProvider();
        var service = new SecretRotationBackgroundService(provider, Substitute.For<ILogger<SecretRotationBackgroundService>>());

        var method = typeof(SecretRotationBackgroundService).GetMethod(
            "CheckExpiredSecretsAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        await (Task)method.Invoke(service, [CancellationToken.None])!;

        auditedUnder.Should().Equal(tenantA, tenantB);
        auditEntries.Should().HaveCount(2);
        auditEntries.Select(entry => entry.Description).Should().OnlyContain(description =>
            !description.Contains("secret-value-must-not-be-audited", StringComparison.Ordinal));
        await configManager.Received(2).GetExpiredSecretsAsync(Arg.Any<TimeSpan?>());
    }
}
