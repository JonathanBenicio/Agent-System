using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.Persistence;
using AgenticSystem.Infrastructure.Persistence.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace AgenticSystem.Tests;

public class PostgresSessionSummaryStoreTests
{
    private readonly IDbContextFactory<AgenticDbContext> _dbContextFactory;
    private readonly PostgresSessionSummaryStore _sut;
    private readonly DbContextOptions<AgenticDbContext> _options;
    private readonly ITenantContextAccessor _tenantAccessor;

    public PostgresSessionSummaryStoreTests()
    {
        var dbName = $"session-summary-tests-{Guid.NewGuid():N}";
        _options = new DbContextOptionsBuilder<AgenticDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        _tenantAccessor = Substitute.For<ITenantContextAccessor>();
        _tenantAccessor.Current.Returns(new TenantContext { TenantId = "test-tenant" });

        // Ensure database is created
        using (var ctx = new AgenticDbContext(_options, _tenantAccessor))
        {
            ctx.Database.EnsureCreated();
        }

        var factory = Substitute.For<IDbContextFactory<AgenticDbContext>>();
        factory.CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(new AgenticDbContext(_options, _tenantAccessor)));

        _dbContextFactory = factory;
        _sut = new PostgresSessionSummaryStore(_dbContextFactory, NullLogger<PostgresSessionSummaryStore>.Instance);
    }

    [Fact]
    public async Task SaveSummaryAsync_PersistsToDatabase()
    {
        // Arrange
        var summary = new SessionSummary
        {
            SessionId = "session-001",
            Summary = "User discussed Kubernetes deployment strategies",
            TopicsDiscussed = new List<string> { "kubernetes", "deployment", "docker" },
            AgentsUsed = new List<string> { "DevOpsAgent" },
            EventCount = 10,
            CreatedAt = DateTime.UtcNow,
        };

        // Act
        await _sut.SaveSummaryAsync(summary, "user-1", "test-tenant");

        // Assert - create fresh context to verify
        await using var verifyCtx = new AgenticDbContext(_options, _tenantAccessor);
        var entities = await verifyCtx.SessionSummaries.IgnoreQueryFilters().ToListAsync();
        entities.Should().HaveCount(1);
        entities[0].SessionId.Should().Be("session-001");
        entities[0].Summary.Should().Contain("Kubernetes");
        entities[0].UserId.Should().Be("user-1");
        entities[0].TenantId.Should().Be("test-tenant");
    }

    [Fact]
    public async Task GetRelevantAsync_NoData_ReturnsEmpty()
    {
        // Act
        var results = await _sut.GetRelevantAsync("nonexistent topic xyz", maxResults: 5);

        // Assert
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task GetRelevantAsync_WithData_ReturnsOrderedByRecency()
    {
        // Arrange
        var oldSummary = new SessionSummary
        {
            SessionId = "session-old",
            Summary = "Old discussion about Python",
            TopicsDiscussed = new List<string> { "python" },
            AgentsUsed = new List<string> { "CodingAgent" },
            EventCount = 5,
            CreatedAt = DateTime.UtcNow.AddDays(-7),
        };

        var newSummary = new SessionSummary
        {
            SessionId = "session-new",
            Summary = "New discussion about Python",
            TopicsDiscussed = new List<string> { "python" },
            AgentsUsed = new List<string> { "CodingAgent" },
            EventCount = 8,
            CreatedAt = DateTime.UtcNow,
        };

        await _sut.SaveSummaryAsync(oldSummary, "user-1", "test-tenant");
        await _sut.SaveSummaryAsync(newSummary, "user-1", "test-tenant");

        // Act
        var results = await _sut.GetRelevantAsync("python", maxResults: 5);

        // Assert
        results.Should().HaveCount(2);
        results[0].SessionId.Should().Be("session-new");
        results[1].SessionId.Should().Be("session-old");
    }

    [Fact]
    public async Task SaveSummaryAsync_MultipleSessions_AllPersisted()
    {
        // Arrange & Act
        for (int i = 0; i < 10; i++)
        {
            var summary = new SessionSummary
            {
                SessionId = $"session-{i}",
                Summary = $"Discussion about topic {i}",
                TopicsDiscussed = new List<string> { $"topic{i}" },
                AgentsUsed = new List<string> { "TestAgent" },
                EventCount = i + 1,
                CreatedAt = DateTime.UtcNow.AddMinutes(-i),
            };
            await _sut.SaveSummaryAsync(summary, "user-1", "test-tenant");
        }

        // Assert
        await using var verifyCtx = new AgenticDbContext(_options, _tenantAccessor);
        var count = await verifyCtx.SessionSummaries.IgnoreQueryFilters().CountAsync();
        count.Should().Be(10);
    }
}
