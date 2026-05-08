using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Core.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace AgenticSystem.Tests;

public class DirectAgentRequestExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_UsesExplicitDirectExecutionService()
    {
        var agentFactory = Substitute.For<IAgentFactory>();
        var directExecutionService = Substitute.For<IDirectAgentExecutionService>();
        var preProcessingPipeline = Substitute.For<IAgentExecutionPreProcessingPipeline>();
        var postProcessingPipeline = Substitute.For<IAgentExecutionPostProcessingPipeline>();
        var sessionManager = Substitute.For<ISessionManager>();
        var runtimeCoordinator = Substitute.For<IAgentRuntimeCoordinator>();
        var logger = Substitute.For<ILogger<DirectAgentRequestExecutor>>();

        runtimeCoordinator.BeginAgentScope(Arg.Any<string>(), Arg.Any<IEnumerable<string>>())
            .Returns(new DisposableScope());
        preProcessingPipeline.ProcessAsync(Arg.Any<AgentExecutionPreProcessingContext>(), Arg.Any<CancellationToken>())
            .Returns(new AgentExecutionPreProcessingResult { EffectiveInput = "enriched hello", AppliedCorrectionRuleCount = 2 });
        postProcessingPipeline.ProcessAsync(Arg.Any<AgentExecutionPostProcessingContext>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.Arg<AgentExecutionPostProcessingContext>().Response);

        var rawAgent = Substitute.For<IAgent>();
        rawAgent.Name.Returns("FinanceAgent");
        rawAgent.Description.Returns("Finance agent");
        rawAgent.Domain.Returns("finance");
        rawAgent.Tier.Returns(AgentTier.Specialist);
        rawAgent.AvailableTools.Returns(Array.Empty<string>());

        directExecutionService.ExecuteDirectAsync(
                rawAgent,
                "session-1",
                "enriched hello",
                Arg.Any<UserContext>(),
                Arg.Any<CancellationToken>())
            .Returns(new AgentResponse
            {
                Content = "Direct response",
                AgentName = "FinanceAgent",
                AgentTier = AgentTier.Specialist,
                Success = true,
                Metadata = new Dictionary<string, object>()
            });

        agentFactory.GetAllAgentsAsync().Returns([
            new AgentInfo
            {
                Name = "FinanceAgent",
                Domain = "finance",
                Tier = AgentTier.Specialist,
                IsActive = true
            }
        ]);
        agentFactory.ResolveAgentAsync(Arg.Any<AgentInfo>()).Returns(rawAgent);

        var sut = new DirectAgentRequestExecutor(
            agentFactory,
            preProcessingPipeline,
            sessionManager,
            runtimeCoordinator,
            postProcessingPipeline,
            logger,
            directAgentExecutionService: directExecutionService);

        var context = new UserContext { UserId = "user-1" };

        var result = await sut.ExecuteAsync("session-1", "hello", context, "FinanceAgent", CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Metadata["executionMode"].Should().Be("direct");
        result.Metadata["appliedCorrectionRules"].Should().Be(2);
        await directExecutionService.Received(1).ExecuteDirectAsync(rawAgent, "session-1", "enriched hello", context, Arg.Any<CancellationToken>());
        await preProcessingPipeline.Received(1).ProcessAsync(
            Arg.Is<AgentExecutionPreProcessingContext>(ctx =>
                ctx.SessionId == "session-1"
                && ctx.TargetAgent == "FinanceAgent"
                && ctx.ValidateRequest
                && ctx.ApplyCorrectionRules),
            Arg.Any<CancellationToken>());
        await postProcessingPipeline.Received(1).ProcessAsync(
            Arg.Is<AgentExecutionPostProcessingContext>(ctx =>
                ctx.DirectRequest
                && ctx.TargetAgent == "FinanceAgent"
                && ctx.Response.Metadata["executionMode"].ToString() == "direct"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenTargetAgentDoesNotExist_ReturnsError()
    {
        var agentFactory = Substitute.For<IAgentFactory>();
        var preProcessingPipeline = Substitute.For<IAgentExecutionPreProcessingPipeline>();
        var postProcessingPipeline = Substitute.For<IAgentExecutionPostProcessingPipeline>();
        var sessionManager = Substitute.For<ISessionManager>();
        var runtimeCoordinator = Substitute.For<IAgentRuntimeCoordinator>();
        var logger = Substitute.For<ILogger<DirectAgentRequestExecutor>>();

        agentFactory.GetAllAgentsAsync().Returns(Array.Empty<AgentInfo>());

        var sut = new DirectAgentRequestExecutor(
            agentFactory,
            preProcessingPipeline,
            sessionManager,
            runtimeCoordinator,
            postProcessingPipeline,
            logger);

        var result = await sut.ExecuteAsync("session-1", "hello", new UserContext { UserId = "user-1" }, "FinanceAgent", CancellationToken.None);

        result.Success.Should().BeFalse();
        result.AgentName.Should().Be(nameof(DirectAgentRequestExecutor));
        result.Content.Should().Contain("não encontrado");
        await preProcessingPipeline.DidNotReceive().ProcessAsync(Arg.Any<AgentExecutionPreProcessingContext>(), Arg.Any<CancellationToken>());
        await postProcessingPipeline.DidNotReceive().ProcessAsync(Arg.Any<AgentExecutionPostProcessingContext>(), Arg.Any<CancellationToken>());
    }

    private sealed class DisposableScope : IDisposable
    {
        public void Dispose()
        {
        }
    }
}