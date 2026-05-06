using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Core.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace AgenticSystem.Tests;

public class AgentExecutionWorkflowDirectExecutionTests
{
    [Fact]
    public async Task ExecuteDirectAsync_UsesExplicitDirectExecutionFactory()
    {
        var directRequestExecutor = Substitute.For<IDirectAgentRequestExecutor>();
        var sessionManager = Substitute.For<ISessionManager>();
        var runtimeCoordinator = Substitute.For<IAgentRuntimeCoordinator>();
        var runtimeContextAccessor = Substitute.For<ILLMRuntimeContextAccessor>();
        var frameworkOrchestrator = Substitute.For<IFrameworkOrchestratorService>();
        var logger = Substitute.For<ILogger<AgentExecutionWorkflow>>();

        runtimeContextAccessor.BeginScope(Arg.Any<UserContext>(), Arg.Any<string?>())
            .Returns(new DisposableScope());
        directRequestExecutor.ExecuteAsync("session-1", "hello", Arg.Any<UserContext>(), "FinanceAgent", Arg.Any<CancellationToken>())
            .Returns(new AgentResponse
            {
                Content = "Direct response",
                AgentName = "FinanceAgent",
                AgentTier = AgentTier.Specialist,
                Success = true,
                Metadata = new Dictionary<string, object>()
            });

        var sut = new AgentExecutionWorkflow(
            directRequestExecutor,
            sessionManager,
            runtimeCoordinator,
            runtimeContextAccessor,
            frameworkOrchestrator,
            logger);

        var context = new UserContext { UserId = "user-1" };

        var result = await sut.ExecuteDirectAsync("session-1", "hello", context, "FinanceAgent", CancellationToken.None);

        result.Success.Should().BeTrue();
        await directRequestExecutor.Received(1).ExecuteAsync("session-1", "hello", context, "FinanceAgent", Arg.Any<CancellationToken>());
    }

    private sealed class DisposableScope : IDisposable
    {
        public void Dispose()
        {
        }
    }
}