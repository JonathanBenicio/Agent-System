using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Core.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace AgenticSystem.Tests;

public class AgentExecutionPostProcessingPipelineTests
{
    [Fact]
    public async Task ProcessAsync_PersistsConfidenceAndMemory()
    {
        var sessionManager = Substitute.For<ISessionManager>();
        var runtimeCoordinator = Substitute.For<IAgentRuntimeCoordinator>();
        var confidenceCalculator = Substitute.For<IConfidenceScoreCalculator>();
        var reflectionEngine = Substitute.For<IReflectionEngine>();
        var correctionLoop = Substitute.For<ICorrectionLoop>();
        var agentMemoryService = Substitute.For<IAgentMemoryService>();
        var logger = Substitute.For<ILogger<AgentExecutionPostProcessingPipeline>>();

        var reflection = new Reflection
        {
            SessionId = "session-1",
            AgentName = "FinanceAgent",
            ImprovementSuggestion = "Adicionar fontes",
            ConfidenceInOutcome = 0.82
        };

        reflectionEngine.ReflectAsync("session-1", "FinanceAgent", "hello", "processed", 0.85)
            .Returns(reflection);
        reflectionEngine.GetSessionReflectionsAsync("session-1")
            .Returns(Task.FromResult<IEnumerable<Reflection>>(new[] { reflection }));
        confidenceCalculator.Calculate(
                Arg.Any<AgentResponse>(),
                Arg.Any<RAGContext?>(),
                Arg.Any<IEnumerable<Reflection>?>(),
                Arg.Any<ToolAvailabilityResult?>())
            .Returns(new ConfidenceScore
            {
                Value = 0.91,
                Level = ConfidenceLevel.High,
                Label = "Alta"
            });
        correctionLoop.AddRuleAsync("user-1", "Adicionar fontes", "auto-reflection", "FinanceAgent")
            .Returns(new CorrectionRule());

        var sut = new AgentExecutionPostProcessingPipeline(
            sessionManager,
            runtimeCoordinator,
            confidenceCalculator,
            logger,
            reflectionEngine: reflectionEngine,
            correctionLoop: correctionLoop,
            agentMemoryService: agentMemoryService);

        var context = CreateContext();

        var result = await sut.ProcessAsync(context, CancellationToken.None);

        result.Confidence.Should().NotBeNull();
        result.Confidence!.Value.Should().Be(0.91);
        await sessionManager.Received(1).AddEventAsync(
            "session-1",
            Arg.Is<AgentEvent>(ev =>
                ev.AgentName == "FinanceAgent"
                && ev.Context["executionMode"].ToString() == "direct"
                && ev.Context.ContainsKey("analysis")));
        await runtimeCoordinator.Received(1).RecordArtifactAsync(
            Arg.Is<AgentExecutionArtifact>(artifact =>
                artifact.SessionId == "session-1"
                && artifact.AgentName == "FinanceAgent"
                && artifact.Name == "WorkflowOutcome"),
            Arg.Any<CancellationToken>());
        await sessionManager.Received(1).ConsolidateSessionAsync("session-1");
        await agentMemoryService.Received(1).RecordInteractionAsync(
            "session-1",
            "FinanceAgent",
            context.UserContext,
            "hello",
            result,
            reflection,
            Arg.Any<CancellationToken>());
        await correctionLoop.Received(1).AddRuleAsync(
            "user-1",
            "Adicionar fontes",
            "auto-reflection",
            "FinanceAgent");
    }

    [Fact]
    public async Task ProcessAsync_WhenApprovalRequired_ReturnsPendingResponseWithoutPersistence()
    {
        var sessionManager = Substitute.For<ISessionManager>();
        var runtimeCoordinator = Substitute.For<IAgentRuntimeCoordinator>();
        var confidenceCalculator = Substitute.For<IConfidenceScoreCalculator>();
        var finalApprovalService = Substitute.For<IFinalResponseApprovalService>();
        var agentMemoryService = Substitute.For<IAgentMemoryService>();
        var logger = Substitute.For<ILogger<AgentExecutionPostProcessingPipeline>>();

        confidenceCalculator.Calculate(
                Arg.Any<AgentResponse>(),
                Arg.Any<RAGContext?>(),
                Arg.Any<IEnumerable<Reflection>?>(),
                Arg.Any<ToolAvailabilityResult?>())
            .Returns(new ConfidenceScore
            {
                Value = 0.55,
                Level = ConfidenceLevel.Medium,
                Label = "Média"
            });
        finalApprovalService.EvaluateAsync(
                "session-1",
                "hello",
                Arg.Any<AnalysisResult>(),
                Arg.Any<AgentResponse>(),
                Arg.Any<CancellationToken>())
            .Returns(new FinalResponseApprovalDecision
            {
                Allowed = false,
                RequiresApproval = true,
                Reason = "Precisa de revisão humana",
                ApprovalRequest = new FinalResponseApprovalRequest
                {
                    Id = "approval-1",
                    SessionId = "session-1",
                    AgentName = "FinanceAgent",
                    Reason = "Precisa de revisão humana"
                }
            });

        var sut = new AgentExecutionPostProcessingPipeline(
            sessionManager,
            runtimeCoordinator,
            confidenceCalculator,
            logger,
            finalApprovalService: finalApprovalService,
            agentMemoryService: agentMemoryService);

        var result = await sut.ProcessAsync(CreateContext(), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Metadata["pendingFinalApproval"].Should().Be(true);
        result.Metadata["finalApprovalId"].Should().Be("approval-1");
        await sessionManager.Received(1).AddEventAsync(
            "session-1",
            Arg.Is<AgentEvent>(ev => ev.Context.ContainsKey("pendingFinalApproval")));
        await runtimeCoordinator.DidNotReceive().RecordArtifactAsync(Arg.Any<AgentExecutionArtifact>(), Arg.Any<CancellationToken>());
        await sessionManager.DidNotReceive().ConsolidateSessionAsync(Arg.Any<string>());
        await agentMemoryService.DidNotReceive().RecordInteractionAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<UserContext>(),
            Arg.Any<string>(),
            Arg.Any<AgentResponse>(),
            Arg.Any<Reflection?>(),
            Arg.Any<CancellationToken>());
    }

    private static AgentExecutionPostProcessingContext CreateContext()
    {
        return new AgentExecutionPostProcessingContext
        {
            SessionId = "session-1",
            Input = "hello",
            UserContext = new UserContext { UserId = "user-1" },
            Analysis = new AnalysisResult
            {
                Intent = IntentType.Chat,
                PrimaryDomain = "finance",
                RecommendedTier = AgentTier.Specialist,
                EstimatedAgent = "FinanceAgent",
                Confidence = 1
            },
            Response = new AgentResponse
            {
                Content = "processed",
                AgentName = "FinanceAgent",
                AgentTier = AgentTier.Specialist,
                Success = true,
                Metadata = new Dictionary<string, object>
                {
                    ["executionMode"] = "direct"
                }
            },
            Latency = TimeSpan.FromMilliseconds(250),
            DirectRequest = true,
            TargetAgent = "FinanceAgent"
        };
    }
}