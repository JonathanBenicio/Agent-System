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
    public async Task ExecuteAsync_UsesExplicitDirectExecutionFactory()
    {
        var agentFactory = Substitute.For<IAgentFactory>();
        var directExecutionFactory = Substitute.For<IDirectAgentExecutionFactory>();
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

        var executableAgent = Substitute.For<IAgent>();
        executableAgent.Name.Returns("FinanceAgent");
        executableAgent.Description.Returns("Finance agent");
        executableAgent.Domain.Returns("finance");
        executableAgent.Tier.Returns(AgentTier.Specialist);
        executableAgent.AvailableTools.Returns(Array.Empty<string>());
        executableAgent.ExecuteAsync(Arg.Any<string>(), Arg.Any<UserContext>())
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
        directExecutionFactory.CreateDirectExecutionAgentAsync(rawAgent, Arg.Any<CancellationToken>())
            .Returns(executableAgent);

        var sut = new DirectAgentRequestExecutor(
            agentFactory,
            preProcessingPipeline,
            sessionManager,
            runtimeCoordinator,
            postProcessingPipeline,
            logger,
            directAgentExecutionFactory: directExecutionFactory);

        var context = new UserContext { UserId = "user-1" };

        var result = await sut.ExecuteAsync("session-1", "hello", context, "FinanceAgent", CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Metadata["executionMode"].Should().Be("direct");
        result.Metadata["appliedCorrectionRules"].Should().Be(2);
        await directExecutionFactory.Received(1).CreateDirectExecutionAgentAsync(rawAgent, Arg.Any<CancellationToken>());
        await preProcessingPipeline.Received(1).ProcessAsync(
            Arg.Is<AgentExecutionPreProcessingContext>(ctx =>
                ctx.SessionId == "session-1"
                && ctx.TargetAgent == "FinanceAgent"
                && ctx.ValidateRequest
                && ctx.ApplyCorrectionRules),
            Arg.Any<CancellationToken>());
        await executableAgent.Received(1).ExecuteAsync("enriched hello", context);
        await postProcessingPipeline.Received(1).ProcessAsync(
            Arg.Is<AgentExecutionPostProcessingContext>(ctx =>
                ctx.DirectRequest
                && ctx.TargetAgent == "FinanceAgent"
                && ctx.Response.Metadata["executionMode"].ToString() == "direct"),
            Arg.Any<CancellationToken>());
        await rawAgent.DidNotReceive().ExecuteAsync(Arg.Any<string>(), Arg.Any<UserContext>());
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