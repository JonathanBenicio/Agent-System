using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Core.Services;
using AgenticSystem.Infrastructure.Persistence;
using AgenticSystem.Infrastructure.Persistence.Entities;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Tests;

public class PlatformConfigStoreTests
{
    [Fact]
    public async Task PostgresPlatformConfigStore_IsGlobalEncryptsSecretsAndAuditsOnlyHashes()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var tenantContext = new TenantContextAccessor();
        var options = new DbContextOptionsBuilder<AgenticDbContext>().UseSqlite(connection).Options;
        var factory = new FakeDbContextFactory
        {
            ContextCreator = () => new AgenticDbContext(options, tenantContext)
        };
        await using (var initialDb = factory.CreateDbContext())
            await initialDb.Database.EnsureCreatedAsync();

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

        await store.SetValuesAsync(
            [
                new PlatformConfigValue("llm.providers.openai.apiKey", "platform-secret", IsSecret: true),
                new PlatformConfigValue("llm.providers.openai.enabled", bool.TrueString)
            ],
            "platform-admin-a");

        (await store.GetValueAsync("llm.providers.openai.apiKey")).Should().Be("platform-secret");
        (await store.GetValueAsync("llm.providers.openai.enabled")).Should().Be(bool.TrueString);
        await using var db = factory.CreateDbContext();
        var platformSetting = await db.PlatformConfigs.SingleAsync(setting => setting.Key == "llm.providers.openai.apiKey");
        platformSetting.Value.Should().Be("********");
        platformSetting.EncryptedValue.Should().Be("cipher:platform-secret");
        platformSetting.ChangedBy.Should().Be("platform-admin-a");

        var tenantConfigCount = await db.ConfigEntries.IgnoreQueryFilters().CountAsync();
        tenantConfigCount.Should().Be(0);
        var audit = await db.PlatformConfigAudits.SingleAsync(item => item.Key == "llm.providers.openai.apiKey");
        audit.ChangedBy.Should().Be("platform-admin-a");
        audit.NewValueHash.Should().NotBe("platform-secret");
        audit.NewValueHash.Should().NotBe("cipher:platform-secret");
        audit.PreviousValueHash.Should().BeNull();

        reloadNotifier.Received(1).NotifyChange("llm.providers.openai.apiKey");
        reloadNotifier.Received(1).NotifyChange("llm.providers.openai.enabled");
    }
}
