using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Core.Services;
using AgenticSystem.Infrastructure.Persistence;
using AgenticSystem.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Pgvector.EntityFrameworkCore;

namespace AgenticSystem.Tests;

public sealed class DynamicAgentSpecificationPersistenceTests
{
    [Fact]
    public async Task EfRepositoryPreservesEnvelopeAndLegacyColumnsAcrossContexts()
    {
        var tenant = new TenantContextAccessor();
        var options = new DbContextOptionsBuilder<AgenticDbContext>()
            .UseInMemoryDatabase($"agent-contract-{Guid.NewGuid():N}").Options;
        await CheckRoundtrip(options, tenant, migrate: false);
    }

    [RequiresPostgresFact]
    public async Task PostgreSqlPreservesSpecificationWithMigrationAndTenantIsolation()
    {
        var connection = Environment.GetEnvironmentVariable("AGENTIC_TEST_POSTGRES")
            ?? throw new InvalidOperationException("AGENTIC_TEST_POSTGRES is required for PostgreSQL validation.");
        var target = new NpgsqlConnectionStringBuilder(connection);
        if (target.Host != "127.0.0.1" || target.Port != 55432 ||
            string.IsNullOrWhiteSpace(target.Database) ||
            !target.Database.StartsWith("review_pr152_", StringComparison.Ordinal) || target.Username != "validation")
            throw new InvalidOperationException("Only an isolated review_pr152_* database on backend-validation Compose port 55432 is accepted.");
        var options = new DbContextOptionsBuilder<AgenticDbContext>()
            .UseNpgsql(connection, pg => { pg.UseVector(); pg.MigrationsHistoryTable("__ef_migrations_history"); }).Options;
        await CheckRoundtrip(options, new TenantContextAccessor(), migrate: true);
    }

    private static async Task CheckRoundtrip(DbContextOptions<AgenticDbContext> options,
        TenantContextAccessor tenant, bool migrate)
    {
        var id = $"agent-contract-{Guid.NewGuid():N}";
        var otherTenant = $"agent-other-{Guid.NewGuid():N}";
        using var tenantScope = tenant.BeginScope(new TenantContext { TenantId = id });
        await using (var setup = new AgenticDbContext(options, tenant))
        {
            if (migrate) await setup.Database.MigrateAsync();
            else await setup.Database.EnsureCreatedAsync();
        }
        var store = new PostgresDynamicAgentRepository(new Factory(options, tenant),
            NullLogger<PostgresDynamicAgentRepository>.Instance);
        var spec = new AgentSpecification { Name = "contract-agent", Description = "description",
            Domain = "finance", Tier = AgentTier.Specialist, Instructions = "quoted: \"prompt\"\nsecond line",
            Capabilities = ["code-review"], AllowedTools = ["tool-a"], AutonomyLevel = AutonomyLevel.Assisted,
            Configuration = new() { ["maxConcurrency"] = 7, ["timeoutSeconds"] = 123, ["custom"] = "retained" },
            PolicyIds = ["policy-a"], WorkflowTemplate = "flow-a", AutoCleanupAfter = TimeSpan.FromDays(2) };
        try
        {
            await store.SaveAsync(spec);
            var reopened = await store.GetByNameAsync(spec.Name);
            reopened!.Instructions.Should().Be(spec.Instructions);
            reopened.Capabilities.Should().Equal(spec.Capabilities);
            reopened.PolicyIds.Should().Equal(spec.PolicyIds);
            reopened.Configuration["maxConcurrency"].ToString().Should().Be("7");
            reopened.Configuration["timeoutSeconds"].ToString().Should().Be("123");
            reopened.Configuration["custom"].ToString().Should().Be("retained");
            reopened.WorkflowTemplate.Should().Be("flow-a");
            reopened.AutoCleanupAfter.Should().Be(TimeSpan.FromDays(2));
            using (tenant.BeginScope(new TenantContext { TenantId = otherTenant }))
                (await store.GetByNameAsync(spec.Name)).Should().BeNull();
            await using (var legacy = new AgenticDbContext(options, tenant))
            {
                legacy.DynamicAgents.Add(new DynamicAgentEntity { Name = "legacy", TenantId = id,
                    Description = "legacy description", Domain = "legacy", Instructions = "legacy instructions",
                    Tier = (int)AgentTier.Support, AllowedToolsJson = "[\"legacy-tool\"]" });
                await legacy.SaveChangesAsync();
            }
            var old = await store.GetByNameAsync("legacy");
            old!.Instructions.Should().Be("legacy instructions");
            old.AllowedTools.Should().Equal("legacy-tool");
            old.Capabilities.Should().BeEmpty();
            old.Configuration.Should().BeEmpty();
        }
        finally
        {
            await using var cleanup = new AgenticDbContext(options, tenant);
            cleanup.DynamicAgents.RemoveRange(await cleanup.DynamicAgents.Where(a => a.TenantId == id).ToListAsync());
            await cleanup.SaveChangesAsync();
        }
    }

    private sealed class Factory(DbContextOptions<AgenticDbContext> options, ITenantContextAccessor tenant)
        : IDbContextFactory<AgenticDbContext>
    {
        public AgenticDbContext CreateDbContext() => new(options, tenant);
        public Task<AgenticDbContext> CreateDbContextAsync(CancellationToken ct = default) => Task.FromResult(CreateDbContext());
    }
}
