using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.Extensions;
using AgenticSystem.Infrastructure.Gateway;
using AgenticSystem.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace AgenticSystem.Tests;

public class PostgresPersistenceExtensionsTests
{
    [Fact]
    public void UsePostgresVectorStore_ReplacesInMemoryRegistration()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IVectorStore>(Substitute.For<IVectorStore>());

        services.UsePostgresVectorStore("Host=localhost;Database=test");

        var provider = services.BuildServiceProvider();
        var store = provider.GetRequiredService<IVectorStore>();
        store.Should().BeOfType<PostgresVectorStore>();
    }

    [Fact]
    public void UsePostgresCostTracker_ReplacesInMemoryRegistration()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ICostTracker, CostTracker>();

        services.UsePostgresCostTracker("Host=localhost;Database=test");

        var provider = services.BuildServiceProvider();
        var tracker = provider.GetRequiredService<ICostTracker>();
        tracker.Should().BeOfType<PostgresCostTracker>();
    }

    [Fact]
    public void UsePostgresSmartRouter_DecoratesExistingSmartRouter()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(Substitute.For<IUserPreferenceEngine>());
        services.AddSingleton<ISmartRouter, AgenticSystem.Core.Services.SmartRouter>();

        services.UsePostgresSmartRouter("Host=localhost;Database=test");

        var provider = services.BuildServiceProvider();
        var router = provider.GetRequiredService<ISmartRouter>();
        router.Should().BeOfType<PersistentSmartRouter>();
    }

    [Fact]
    public void UsePostgresVectorStore_WithNoExistingRegistration_AddsNew()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.UsePostgresVectorStore("Host=localhost;Database=test");

        var provider = services.BuildServiceProvider();
        var store = provider.GetRequiredService<IVectorStore>();
        store.Should().BeOfType<PostgresVectorStore>();
    }

    [Fact]
    public void UsePostgresCostTracker_WithNoExistingRegistration_AddsNew()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.UsePostgresCostTracker("Host=localhost;Database=test");

        var provider = services.BuildServiceProvider();
        var tracker = provider.GetRequiredService<ICostTracker>();
        tracker.Should().BeOfType<PostgresCostTracker>();
    }
}

public class PersistentSmartRouterTests
{
    private readonly ISmartRouter _innerRouter;
    private readonly PersistentSmartRouter _sut;

    public PersistentSmartRouterTests()
    {
        _innerRouter = Substitute.For<ISmartRouter>();
        var logger = Substitute.For<ILogger<PersistentSmartRouter>>();
        var options = new DbContextOptionsBuilder<AgenticDbContext>()
            .UseInMemoryDatabase($"smart-router-tests-{Guid.NewGuid():N}")
            .Options;
        var factory = Substitute.For<IDbContextFactory<AgenticDbContext>>();
        factory.CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(new AgenticDbContext(options)));

        _sut = new PersistentSmartRouter(_innerRouter, factory, logger);
    }

    [Fact]
    public async Task RouteAsync_DelegatesToInner()
    {
        var analysis = new AnalysisResult { PrimaryDomain = "code", EstimatedAgent = "Agent1", Confidence = 0.9 };
        var context = new UserContext { UserId = "user1" };
        var expected = new RoutingDecision { PrimaryAgent = "Agent1", ConfidenceScore = 0.9 };

        _innerRouter.RouteAsync(analysis, context).Returns(expected);

        var result = await _sut.RouteAsync(analysis, context);

        result.Should().Be(expected);
        await _innerRouter.Received(1).RouteAsync(analysis, context);
    }

    [Fact]
    public async Task RecordPerformanceAsync_DelegatesToInner()
    {
        var metric = new AgentPerformanceMetric
        {
            Domain = "code",
            Latency = TimeSpan.FromMilliseconds(100),
            Success = true,
            RecordedAt = DateTime.UtcNow
        };

        await _sut.RecordPerformanceAsync("Agent1", metric);

        await _innerRouter.Received(1).RecordPerformanceAsync("Agent1", metric);
    }

    [Fact]
    public async Task GetRankingsByDomainAsync_DelegatesToInner()
    {
        var rankings = new List<AgentRanking>
        {
            new() { AgentName = "Agent1", Score = 0.9 }
        };
        _innerRouter.GetRankingsByDomainAsync("code").Returns(rankings);

        var result = await _sut.GetRankingsByDomainAsync("code");

        result.Should().BeEquivalentTo(rankings);
        await _innerRouter.Received().GetRankingsByDomainAsync("code");
    }
}

public class ICostTrackerInterfaceTests
{
    [Fact]
    public void CostTracker_ImplementsICostTracker()
    {
        var tracker = new CostTracker();
        tracker.Should().BeAssignableTo<ICostTracker>();
    }

    [Fact]
    public void ICostTracker_RecordAndRetrieveCost_WorksViaInterface()
    {
        ICostTracker tracker = new CostTracker(defaultDailyBudget: 100m);

        tracker.RecordCost("svc1", "cat1", 10m);
        tracker.GetServiceCost("svc1").Should().Be(10m);
    }

    [Fact]
    public void ICostTracker_SetBudgetAndGetReport_WorksViaInterface()
    {
        ICostTracker tracker = new CostTracker();

        tracker.SetBudget("svc1", 50m);
        tracker.RecordCost("svc1", "llm", 40m);

        var report = tracker.GetReport();
        report.BudgetAlert.Should().BeTrue(); // 40/50 = 80%
    }
}
