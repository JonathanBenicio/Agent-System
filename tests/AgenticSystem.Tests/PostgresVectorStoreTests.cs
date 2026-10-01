using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.Persistence;
using AgenticSystem.Infrastructure.Persistence.Entities;

namespace AgenticSystem.Tests;

public class PostgresVectorStoreTests
{
    private class TestAgenticDbContext : AgenticDbContext
    {
        public TestAgenticDbContext(DbContextOptions<AgenticDbContext> options, ITenantContextAccessor tenantContext)
            : base(options, tenantContext)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            // Empty-room guard is provider independent; other room SQL requires PostgreSQL.
        }
    }

    [Fact]
    public async Task SearchWithFiltersAsync_EmptyRoomListReturnsNoDocuments()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
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

    [Fact(Skip = "Raw SQL ANY() array syntax requires actual Postgres instance")]
    public async Task SearchWithFiltersAsync_PreFiltersRoomsAtSqlLevel_ReturnsAuthorizedDocuments()
    {
        // Arrange
        using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        // Register custom sqlite function for jsonb_extract_path_text
        connection.CreateFunction("jsonb_extract_path_text", (string json, string path) =>
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty(path, out var prop))
                {
                    return prop.GetString();
                }
            }
            catch
            {
                // Fallback
            }
            return null;
        });

        var tenantAccessor = Substitute.For<ITenantContextAccessor>();
        tenantAccessor.CurrentTenantId.Returns("test-tenant");

        var options = new DbContextOptionsBuilder<AgenticDbContext>()
            .UseSqlite(connection)
            .Options;

        // Initialize schema
        using (var ctx = new TestAgenticDbContext(options, tenantAccessor))
        {
            await ctx.Database.EnsureCreatedAsync();

            // Seed 55 unauthorized documents (with newer timestamp, so they are returned first if SQL filter is not applied)
            for (int i = 0; i < 55; i++)
            {
                ctx.VectorDocuments.Add(new VectorDocumentEntity
                {
                    Id = $"unauth-{i}",
                    TenantId = "test-tenant",
                    Content = $"Unauthorized document content {i}",
                    Type = "document",
                    Collection = "default",
                    MetadataJson = "{\"room_id\":\"room-unauthorized\"}",
                    IndexedAt = DateTime.UtcNow
                });
            }

            // Seed 5 authorized documents (with older timestamp)
            for (int i = 0; i < 5; i++)
            {
                ctx.VectorDocuments.Add(new VectorDocumentEntity
                {
                    Id = $"auth-{i}",
                    TenantId = "test-tenant",
                    Content = $"Authorized document content {i}",
                    Type = "document",
                    Collection = "default",
                    MetadataJson = "{\"room_id\":\"room-authorized\"}",
                    IndexedAt = DateTime.UtcNow.AddMinutes(-10)
                });
            }

            await ctx.SaveChangesAsync();
        }

        var dbFactory = new FakeDbContextFactory
        {
            ContextCreator = () => new TestAgenticDbContext(options, tenantAccessor)
        };

        var store = new PostgresVectorStore(dbFactory, NullLogger<PostgresVectorStore>.Instance, embeddingGenerator: null);

        var filters = new Dictionary<string, string>
        {
            { "room_ids", "room-authorized" }
        };

        // Act
        var result = await store.SearchWithFiltersAsync(query: "*", filters: filters);

        // Assert
        result.Matches.Should().HaveCount(5);
        result.Matches.Should().OnlyContain(m => m.Id.StartsWith("auth-"));
    }
}
