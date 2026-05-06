using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.AgentFramework;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace AgenticSystem.Tests;

public class FrameworkOrchestratorServicePostProcessingTests
{
    [Fact]
    public async Task PostProcessHostedResponseAsync_DelegatesToSharedPostProcessingPipeline()
    {
        var calledAgent = Substitute.For<IAgent>();
        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        var preProcessingPipeline = Substitute.For<IAgentExecutionPreProcessingPipeline>();
        var runtimeCoordinator = Substitute.For<IAgentRuntimeCoordinator>();
        var runtimeContextAccessor = Substitute.For<ILLMRuntimeContextAccessor>();
        var postProcessingPipeline = Substitute.For<IAgentExecutionPostProcessingPipeline>();
        var logger = Substitute.For<ILogger<FrameworkOrchestratorService>>();

        calledAgent.Name.Returns("FinanceAgent");
        calledAgent.Domain.Returns("finance");
        calledAgent.Tier.Returns(AgentTier.Specialist);
        calledAgent.AvailableTools.Returns(["ledger.read"]);
        postProcessingPipeline.ProcessAsync(Arg.Any<AgentExecutionPostProcessingContext>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var context = callInfo.Arg<AgentExecutionPostProcessingContext>();
                context.Response.Metadata["postProcessed"] = true;
                return context.Response;
            });

        var sut = new FrameworkOrchestratorService(
            OrchestratorMetadata.Default,
            scopeFactory,
            preProcessingPipeline,
            runtimeCoordinator,
            runtimeContextAccessor,
            postProcessingPipeline,
            logger);

        var result = await sut.PostProcessHostedResponseAsync(
            "session-1",
            "hello",
            new UserContext { UserId = "user-1" },
            "Hosted response",
            calledAgent,
            "FinanceAgent",
            "framework-id-1",
            TimeSpan.FromMilliseconds(120),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Metadata["executionMode"].Should().Be("framework-orchestration");
        result.Metadata["delegatedTo"].Should().Be("FinanceAgent");
        result.Metadata["postProcessed"].Should().Be(true);
        await postProcessingPipeline.Received(1).ProcessAsync(
            Arg.Is<AgentExecutionPostProcessingContext>(ctx =>
                !ctx.ValidateResponse
                && ctx.RunReflection
                && ctx.EventContext["hostingMode"].ToString() == "native"
                && ctx.Response.AgentName == "FinanceAgent"
                && ctx.Analysis.PrimaryDomain == "finance"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PreProcessHostedInputAsync_DelegatesToSharedPreProcessingPipeline()
    {
        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        var preProcessingPipeline = Substitute.For<IAgentExecutionPreProcessingPipeline>();
        var runtimeCoordinator = Substitute.For<IAgentRuntimeCoordinator>();
        var runtimeContextAccessor = Substitute.For<ILLMRuntimeContextAccessor>();
        var postProcessingPipeline = Substitute.For<IAgentExecutionPostProcessingPipeline>();
        var logger = Substitute.For<ILogger<FrameworkOrchestratorService>>();

        preProcessingPipeline.ProcessAsync(Arg.Any<AgentExecutionPreProcessingContext>(), Arg.Any<CancellationToken>())
            .Returns(new AgentExecutionPreProcessingResult
            {
                EffectiveInput = "global enriched hello",
                AppliedCorrectionRuleCount = 1
            });

        var sut = new FrameworkOrchestratorService(
            OrchestratorMetadata.Default,
            scopeFactory,
            preProcessingPipeline,
            runtimeCoordinator,
            runtimeContextAccessor,
            postProcessingPipeline,
            logger);

        var result = await sut.PreProcessHostedInputAsync(
            "session-1",
            "hello",
            new UserContext { UserId = "user-1" },
            CancellationToken.None);

        result.EffectiveInput.Should().Be("global enriched hello");
        result.AppliedCorrectionRuleCount.Should().Be(1);
        await preProcessingPipeline.Received(1).ProcessAsync(
            Arg.Is<AgentExecutionPreProcessingContext>(ctx =>
                ctx.SessionId == "session-1"
                && ctx.TargetAgent == null
                && ctx.ValidateRequest
                && ctx.ApplyCorrectionRules
                && ctx.Metadata["executionMode"].ToString() == "framework-orchestration"),
            Arg.Any<CancellationToken>());
    }
}