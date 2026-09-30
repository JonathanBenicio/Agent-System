using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using NSubstitute;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Core.Services;

namespace AgenticSystem.Tests;

public class QuotaEnforcerTests
{
    private readonly IMemoryCache _cache;
    private readonly ITenantQuotaRepository _repository;
    private readonly ILogger<QuotaEnforcer> _logger;
    private readonly IEventBus _eventBus;
    private readonly QuotaEnforcer _sut;

    public QuotaEnforcerTests()
    {
        _cache = new MemoryCache(new MemoryCacheOptions());
        _repository = Substitute.For<ITenantQuotaRepository>();
        _logger = Substitute.For<ILogger<QuotaEnforcer>>();
        _eventBus = Substitute.For<IEventBus>();
        _sut = new QuotaEnforcer(_cache, _repository, _logger, eventBus: _eventBus);
    }

    [Fact]
    public async Task CheckQuotaAsync_HappyPath_ReturnsAllowed()
    {
        // Arrange
        var tenantId = "tenant-1";
        var snapshot = new TenantQuotaSnapshot(
            TenantId: tenantId,
            RequestsPerMinute: 60,
            MaxTokensPerDay: 1_000_000,
            MaxDailyBudgetUsd: 50.0,
            CurrentDailyTokens: 100_000,
            CurrentDailyCostUsd: 5.0,
            CurrentDailyRequests: 10
        );

        _repository.GetOrCreateAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns(snapshot);

        // Act
        var result = await _sut.CheckQuotaAsync(tenantId, estimatedTokens: 1000, estimatedCostUsd: 0.1);

        // Assert
        result.Allowed.Should().BeTrue();
        result.UsagePercent.Should().BeLessThan(100);
    }

    [Fact]
    public async Task CheckQuotaAsync_MinuteLimitExceeded_ReturnsBlocked()
    {
        // Arrange
        var tenantId = "tenant-2";
        var snapshot = new TenantQuotaSnapshot(
            TenantId: tenantId,
            RequestsPerMinute: 2, // low request limit
            MaxTokensPerDay: 1_000_000,
            MaxDailyBudgetUsd: 50.0,
            CurrentDailyTokens: 100_000,
            CurrentDailyCostUsd: 5.0,
            CurrentDailyRequests: 10
        );

        _repository.GetOrCreateAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns(snapshot);

        // Record usage to reach the minute limit (2 requests)
        await _sut.RecordUsageAsync(tenantId, tokensUsed: 100, costUsd: 0.01);
        await _sut.RecordUsageAsync(tenantId, tokensUsed: 100, costUsd: 0.01);

        // Act
        var result = await _sut.CheckQuotaAsync(tenantId, estimatedTokens: 100, estimatedCostUsd: 0.01);

        // Assert
        result.Allowed.Should().BeFalse();
        result.DenialReason.Should().Contain("requests per minute");
    }

    [Fact]
    public async Task CheckQuotaAsync_DailyTokensExceeded_ReturnsBlocked()
    {
        // Arrange
        var tenantId = "tenant-3";
        var snapshot = new TenantQuotaSnapshot(
            TenantId: tenantId,
            RequestsPerMinute: 60,
            MaxTokensPerDay: 10_000, // low token limit
            MaxDailyBudgetUsd: 50.0,
            CurrentDailyTokens: 9_500,
            CurrentDailyCostUsd: 5.0,
            CurrentDailyRequests: 10
        );

        _repository.GetOrCreateAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns(snapshot);

        // Act
        var result = await _sut.CheckQuotaAsync(tenantId, estimatedTokens: 1000, estimatedCostUsd: 0.1);

        // Assert
        result.Allowed.Should().BeFalse();
        result.DenialReason.Should().Contain("Daily token quota exceeded");
        result.UsagePercent.Should().Be(95.0); // 9500 / 10000 * 100
    }

    [Fact]
    public async Task CheckQuotaAsync_DailyBudgetExceeded_ReturnsBlocked()
    {
        // Arrange
        var tenantId = "tenant-4";
        var snapshot = new TenantQuotaSnapshot(
            TenantId: tenantId,
            RequestsPerMinute: 60,
            MaxTokensPerDay: 1_000_000,
            MaxDailyBudgetUsd: 10.0, // low budget limit
            CurrentDailyTokens: 100_000,
            CurrentDailyCostUsd: 9.5,
            CurrentDailyRequests: 10
        );

        _repository.GetOrCreateAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns(snapshot);

        // Act
        var result = await _sut.CheckQuotaAsync(tenantId, estimatedTokens: 1000, estimatedCostUsd: 1.0);

        // Assert
        result.Allowed.Should().BeFalse();
        result.DenialReason.Should().Contain("Daily budget exceeded");
        result.UsagePercent.Should().Be(95.0); // 9.5 / 10.0 * 100
    }

    [Fact]
    public async Task CheckQuotaAsync_ZeroValueLimits_DoesNotBlock()
    {
        // Arrange
        var tenantId = "tenant-5";
        var snapshot = new TenantQuotaSnapshot(
            TenantId: tenantId,
            RequestsPerMinute: 60,
            MaxTokensPerDay: 0, // unlimited tokens
            MaxDailyBudgetUsd: 0.0, // unlimited budget
            CurrentDailyTokens: 9_999_999_999, // extremely high token usage
            CurrentDailyCostUsd: 9_999_999.0, // extremely high cost
            CurrentDailyRequests: 10
        );

        _repository.GetOrCreateAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns(snapshot);

        // Act
        var result = await _sut.CheckQuotaAsync(tenantId, estimatedTokens: 1000, estimatedCostUsd: 1.0);

        // Assert
        result.Allowed.Should().BeTrue();
    }

    [Fact]
    public async Task RecordUsageAsync_PublishesOnlyTenantScopedCostSummary()
    {
        await _sut.RecordUsageAsync("tenant-42", tokensUsed: 27, costUsd: 0.004);

        await _eventBus.Received(1).PublishAsync(
            Arg.Is<SystemBusEvent>(busEvent =>
                busEvent.EventType == "FinOps.TurnCostUpdated" &&
                busEvent.TenantId == "tenant-42" &&
                busEvent.Payload.ContainsKey("TurnCostSummary")),
            Arg.Any<CancellationToken>());
    }
}
