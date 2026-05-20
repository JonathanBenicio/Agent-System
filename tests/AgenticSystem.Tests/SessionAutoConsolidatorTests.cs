using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace AgenticSystem.Tests;

public class SessionAutoConsolidatorTests
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ISessionStore _sessionStore;
    private readonly ISessionConsolidator _consolidator;
    private readonly IMemoryInjectionService _memoryInjection;
    private readonly ILogger<SessionAutoConsolidator> _logger;
    private readonly SessionAutoConsolidator _sut;

    public SessionAutoConsolidatorTests()
    {
        _sessionStore = Substitute.For<ISessionStore>();
        _consolidator = Substitute.For<ISessionConsolidator>();
        _memoryInjection = Substitute.For<IMemoryInjectionService>();
        _logger = Substitute.For<ILogger<SessionAutoConsolidator>>();

        var services = new ServiceCollection();
        services.AddSingleton(_sessionStore);
        services.AddSingleton(_consolidator);
        services.AddSingleton(_memoryInjection);
        services.AddSingleton(Substitute.For<ILogger<SessionAutoConsolidator>>());
        _serviceProvider = services.BuildServiceProvider();

        _sut = new SessionAutoConsolidator(_serviceProvider, _logger);
    }

    [Fact]
    public async Task ProcessPending_ConsolidatesSessions_WithEndedAt()
    {
        // Arrange
        var sessions = new List<SessionData>
        {
            new() { Id = "session-001", EndedAt = DateTime.UtcNow, IsConsolidated = false, UserId = "u1", TenantId = "default" },
            new() { Id = "session-002", EndedAt = null, IsConsolidated = false, UserId = "u1", TenantId = "default" }
        };
        _sessionStore.GetByTenantAsync("default", null, Arg.Any<int>()).Returns(sessions);
        
        // Act
        var method = typeof(SessionAutoConsolidator).GetMethod("ProcessPendingSessionsAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        await (Task)method!.Invoke(_sut, new object[] { CancellationToken.None })!;

        // Assert
        await _consolidator.Received(1).SummarizeSessionAsync("session-001", Arg.Any<List<AgentEvent>>(), "u1", "default");
        await _consolidator.DidNotReceive().SummarizeSessionAsync("session-002", Arg.Any<List<AgentEvent>>(), "u1", "default");
        await _sessionStore.Received(1).SaveAsync(Arg.Is<SessionData>(s => s.Id == "session-001" && s.IsConsolidated));
    }
}
