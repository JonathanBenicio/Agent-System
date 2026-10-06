using AgenticSystem.Core.LLM.Models;
using AgenticSystem.Core.Models;
using AgenticSystem.Core.Services;
using AgenticSystem.Infrastructure.LLM;
using AgenticSystem.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Pgvector.EntityFrameworkCore;

namespace AgenticSystem.Tests;

public sealed class PostgresTenantApiKeyServiceTests
{
    [RequiresPostgresFact]
    public async Task TenantKeyIsEncryptedScopedAndNeverReturnedByWriteOrListDtos()
    {
        var connection = GetIsolatedConnectionString();
        var tenant = new TenantContextAccessor();
        var options = new DbContextOptionsBuilder<AgenticDbContext>()
            .UseNpgsql(connection, postgres => postgres.UseVector()).Options;
        var factory = new FakeDbContextFactory { ContextCreator = () => new AgenticDbContext(options, tenant) };
        var encryption = new AesConfigEncryptionService("chat-123-api-key-encryption-key");
        const string tenantId = "chat-123-key-tenant";
        const string userKey = "synthetic-key-must-not-be-returned";
        try
        {
            using (tenant.BeginScope(new TenantContext { TenantId = tenantId }))
            {
                await using var db = factory.CreateDbContext();
                var service = new LLMProviderApiKeyService(db, encryption, NullLogger<LLMProviderApiKeyService>.Instance);
                var created = await service.RegisterKeyAsync("OpenAI", new RegisterApiKeyRequest
                {
                    Name = "validation-key", ApiKey = userKey, IsDefault = true
                });
                System.Text.Json.JsonSerializer.Serialize(created).Should().NotContain(userKey);

                db.ChangeTracker.Clear();
                var stored = await db.ProviderApiKeys.IgnoreQueryFilters()
                    .SingleAsync(item => item.TenantId == tenantId && item.Name == "validation-key");
                stored.EncryptedValue.Should().NotBe(userKey);
                encryption.Decrypt(stored.EncryptedValue).Should().Be(userKey);
                stored.IsDefault.Should().BeTrue();
            }

            using (tenant.BeginScope(new TenantContext { TenantId = "chat-123-other-key-tenant" }))
            {
                await using var db = factory.CreateDbContext();
                var service = new LLMProviderApiKeyService(db, encryption, NullLogger<LLMProviderApiKeyService>.Instance);
                (await service.GetKeysByProviderAsync("OpenAI")).Should().BeEmpty();
            }
        }
        finally
        {
            using var scope = tenant.BeginScope(new TenantContext { TenantId = tenantId });
            await using var db = factory.CreateDbContext();
            var stored = await db.ProviderApiKeys.IgnoreQueryFilters()
                .SingleOrDefaultAsync(item => item.TenantId == tenantId && item.Name == "validation-key");
            if (stored is not null)
            {
                db.ProviderApiKeys.Remove(stored);
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
