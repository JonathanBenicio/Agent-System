using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Core.Services;
using AgenticSystem.Infrastructure.AgentFramework;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace AgenticSystem.Tests;

public class FrameworkAgentChannelServiceTests
{
    [Fact]
    public async Task BuildChannelContextAsync_WhenMessagesExist_IncludesStructuredContext()
    {
        var store = new InMemorySessionStore();
        var consolidator = Substitute.For<ISessionConsolidator>();
        consolidator.SummarizeSessionAsync(Arg.Any<string>(), Arg.Any<List<AgentEvent>>())
            .Returns(new SessionSummary());
        consolidator.ExtractInsightsAsync(Arg.Any<string>(), Arg.Any<List<AgentEvent>>())
            .Returns(new SessionInsights());

        var sessionManager = new SessionManager(
            store,
            consolidator,
            Substitute.For<ILogger<SessionManager>>());

        var sessionId = await sessionManager.StartSessionAsync(new UserContext { UserId = "user-1" });
        var sut = new FrameworkAgentChannelService(sessionManager, Substitute.For<ILogger<FrameworkAgentChannelService>>());

        await sut.PublishAsync(sessionId, "Planner", "AnalysisAgent", "Revisar os resultados das etapas anteriores", AgentChannelKind.Planner);

        var result = await sut.BuildChannelContextAsync(sessionId, "AnalysisAgent", "Analise o problema");

        result.Should().Contain("Native Agent Channel Context");
        result.Should().Contain("Planner");
        result.Should().Contain("Analise o problema");
    }
}