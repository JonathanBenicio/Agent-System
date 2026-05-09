using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Core.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace AgenticSystem.Tests;

public class TenantIsolationTests
{
    private readonly ITenantStore _tenantStore;
    private readonly ISessionStore _sessionStore;
    private readonly IVectorStore _vectorStore;
    private readonly ICostTracker _costTracker;
    private readonly TenantIsolationService _enforcer;

    public TenantIsolationTests()
    {
        _tenantStore = Substitute.For<ITenantStore>();
        _sessionStore = Substitute.For<ISessionStore>();
        _vectorStore = Substitute.For<IVectorStore>();
        _costTracker = Substitute.For<ICostTracker>();
        _enforcer = new TenantIsolationService(
            _tenantStore,
            _sessionStore,
            _vectorStore,
            _costTracker,
            Substitute.For<ILogger<TenantIsolationService>>());
    }

    [Fact]
    public async Task CanStartSessionAsync_ShouldReturnFalse_WhenLimitReached()
    {
        // Arrange
        var tenantId = "limited-tenant";
        _tenantStore.GetByIdAsync(tenantId).Returns(new Tenant { Id = tenantId });
        
        // Mock 10 active sessions
        var sessions = Enumerable.Range(1, 100).Select(i => new SessionData { Id = $"s-{i}" }).ToList();
        _sessionStore.GetByTenantAsync(tenantId).Returns(sessions);

        // Act
        var result = await _enforcer.CanStartSessionAsync(tenantId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task CanStartSessionAsync_ShouldReturnTrue_WhenUnderLimit()
    {
        // Arrange
        var tenantId = "good-tenant";
        _tenantStore.GetByIdAsync(tenantId).Returns(new Tenant { Id = tenantId });
        _sessionStore.GetByTenantAsync(tenantId).Returns(new List<SessionData>());

        // Act
        var result = await _enforcer.CanStartSessionAsync(tenantId);

        // Assert
        result.Should().BeTrue();
    }
}
