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
    private readonly IDynamicAgentRepository _dynamicAgentRepository;
    private readonly TenantIsolationService _enforcer;

    public TenantIsolationTests()
    {
        _tenantStore = Substitute.For<ITenantStore>();
        _sessionStore = new InMemorySessionStore();
        _vectorStore = Substitute.For<IVectorStore>();
        _costTracker = Substitute.For<ICostTracker>();
        _dynamicAgentRepository = Substitute.For<IDynamicAgentRepository>();
        _enforcer = new TenantIsolationService(
            _tenantStore,
            _sessionStore,
            _vectorStore,
            _costTracker,
            _dynamicAgentRepository,
            Substitute.For<ILogger<TenantIsolationService>>());
    }

    [Fact]
    public async Task CanStartSessionAsync_ShouldReturnFalse_WhenLimitReached()
    {
        // Arrange
        var tenantId = "limited-tenant";
        _tenantStore.GetByIdAsync(tenantId).Returns(new Tenant { Id = tenantId });
        
        var sessions = Enumerable.Range(1, 100).Select(i => new SessionData { Id = $"s-{i}", TenantId = tenantId }).ToList();
        foreach (var session in sessions)
            await _sessionStore.SaveAsync(session);

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
        // Act
        var result = await _enforcer.CanStartSessionAsync(tenantId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task CanStartSessionAsync_UsesTenantPlanSessionLimit()
    {
        const string tenantId = "free-tenant";
        _tenantStore.GetByIdAsync(tenantId).Returns(new Tenant { Id = tenantId, Limits = TenantLimits.FreeTier() });
        foreach (var index in Enumerable.Range(0, TenantLimits.FreeTier().MaxConcurrentSessions))
            await _sessionStore.SaveAsync(new SessionData { Id = $"session-{index}", TenantId = tenantId });

        var result = await _enforcer.CanStartSessionAsync(tenantId);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task CanStartSessionAsync_FailsClosedForUnknownTenant()
    {
        _tenantStore.GetByIdAsync("missing-tenant").Returns((Tenant?)null);

        var result = await _enforcer.CanStartSessionAsync("missing-tenant");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task CanIngestDocumentAsync_UsesTenantPlanStorageLimitAndIncomingBytes()
    {
        const string tenantId = "free-tenant";
        var tenant = new Tenant { Id = tenantId, Limits = TenantLimits.FreeTier() };
        _tenantStore.GetByIdAsync(tenantId).Returns(tenant);
        _vectorStore.GetStatsAsync(tenantId, Arg.Any<CancellationToken>()).Returns(new VectorStoreStats
        {
            TenantId = tenantId,
            DocumentCount = 1,
            TotalBytes = (long)tenant.Limits.MaxDocumentsMb * 1024 * 1024
        });

        var result = await _enforcer.CanIngestDocumentAsync(tenantId, newBytesCount: 1);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task CanCreateAgentAsync_UsesTenantPlanAndAllowsUpdatingExistingAgent()
    {
        const string tenantId = "free-tenant";
        var limits = TenantLimits.FreeTier();
        limits.MaxAgents = 1;
        _tenantStore.GetByIdAsync(tenantId).Returns(new Tenant { Id = tenantId, Limits = limits });
        _dynamicAgentRepository.GetByNameAsync("already-configured", Arg.Any<CancellationToken>())
            .Returns(new AgentSpecification { Name = "already-configured" });
        _dynamicAgentRepository.GetByNameAsync("new-agent", Arg.Any<CancellationToken>()).Returns((AgentSpecification?)null);
        _dynamicAgentRepository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns([new AgentSpecification { Name = "already-configured" }]);

        (await _enforcer.CanCreateAgentAsync(tenantId, "new-agent")).Should().BeFalse();
        (await _enforcer.CanCreateAgentAsync(tenantId, "already-configured")).Should().BeTrue();
    }
}
