using AgenticSystem.Core.Services;
using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.Persistence;
using AgenticSystem.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Pgvector.EntityFrameworkCore;

namespace AgenticSystem.Tests;

public sealed class PostgresRoomFilterRegressionTests
{
    [ReviewSessionPostgresFact]
    public async Task RoomAllowListIsAppliedInPostgresBeforeCandidateLimit()
    {
        var connection = Environment.GetEnvironmentVariable("AGENTIC_REVIEW_POSTGRES")
            ?? throw new InvalidOperationException("AGENTIC_REVIEW_POSTGRES is required.");
        var target = new NpgsqlConnectionStringBuilder(connection);
        if (target.Host != "127.0.0.1" || target.Port != 55432 || target.Username != "validation" ||
            string.IsNullOrWhiteSpace(target.Database) || !target.Database.StartsWith("review_pr152_", StringComparison.Ordinal))
            throw new InvalidOperationException("Room filter regression requires an isolated review_pr152_* database on port 55432.");

        var tenantId = $"room-filter-{Guid.NewGuid():N}";
        var tenantAccessor = new TenantContextAccessor();
        using var tenantScope = tenantAccessor.BeginScope(new TenantContext { TenantId = tenantId });
        var options = new DbContextOptionsBuilder<AgenticDbContext>()
            .UseNpgsql(target.ConnectionString, postgres =>
            {
                postgres.UseVector();
                postgres.MigrationsHistoryTable("__ef_migrations_history");
            }).Options;
        var factory = new VectorDbContextFactory(options, tenantAccessor);
        await using (var setup = factory.CreateDbContext())
            await setup.Database.MigrateAsync();

        var prefix = $"room-filter-{Guid.NewGuid():N}";
        var ids = Enumerable.Range(0, 60).Select(index => $"{prefix}-{index}").ToArray();
        try
        {
            await using (var seed = factory.CreateDbContext())
            {
                seed.VectorDocuments.AddRange(Enumerable.Range(0, 60).Select(index => new VectorDocumentEntity
                {
                    Id = ids[index],
                    TenantId = tenantId,
                    Content = index < 55 ? "recent unauthorized room material" : "older authorized room material",
                    Type = "document",
                    Collection = "default",
                    MetadataJson = index < 55 ? "{\"room_id\":\"room-unapproved\"}" : "{\"room_id\":\"room-approved\"}",
                    IndexedAt = index < 55 ? DateTime.UtcNow : DateTime.UtcNow.AddMinutes(-10)
                }));
                await seed.SaveChangesAsync();
            }

            var store = new PostgresVectorStore(factory, NullLogger<PostgresVectorStore>.Instance, embeddingGenerator: null);
            var result = await store.SearchWithFiltersAsync("*", new Dictionary<string, string> { ["room_ids"] = "room-approved" });

            result.Matches.Should().HaveCount(5);
            result.Matches.Select(match => match.Id).Should().BeEquivalentTo(ids.Skip(55));
            result.Matches.Should().NotContain(match => match.Content.Contains("unapproved", StringComparison.Ordinal));
        }
        finally
        {
            await using var cleanup = factory.CreateDbContext();
            await cleanup.VectorDocuments.IgnoreQueryFilters().Where(document => ids.Contains(document.Id)).ExecuteDeleteAsync();
        }
    }

    private sealed class VectorDbContextFactory(DbContextOptions<AgenticDbContext> options, TenantContextAccessor tenantAccessor)
        : IDbContextFactory<AgenticDbContext>
    {
        public AgenticDbContext CreateDbContext() => new(options, tenantAccessor);

        public Task<AgenticDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDbContext());
    }
}
