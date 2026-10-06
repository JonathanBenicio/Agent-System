using AgenticSystem.Core.Interfaces;
using AgenticSystem.Infrastructure.Persistence;
using AgenticSystem.Infrastructure.Persistence.Entities;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace AgenticSystem.Tests;

public class PostgresVectorStoreTests
{
    private sealed class TestAgenticDbContext(DbContextOptions<AgenticDbContext> options, ITenantContextAccessor tenantContext)
        : AgenticDbContext(options, tenantContext) { }

    [Fact]
    public async Task SearchWithFiltersAsync_EmptyRoomListReturnsNoDocuments()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var tenantAccessor = Substitute.For<ITenantContextAccessor>();
        tenantAccessor.CurrentTenantId.Returns("test-tenant");
        var options = new DbContextOptionsBuilder<AgenticDbContext>().UseSqlite(connection).Options;
        var factory = new FakeDbContextFactory
        {
            ContextCreator = () => new TestAgenticDbContext(options, tenantAccessor)
        };
        var store = new PostgresVectorStore(factory, NullLogger<PostgresVectorStore>.Instance, embeddingGenerator: null);

        var result = await store.SearchWithFiltersAsync("query", new Dictionary<string, string> { ["room_ids"] = " , " });

        result.Matches.Should().BeEmpty();
        result.TotalFound.Should().Be(0);
    }
}
