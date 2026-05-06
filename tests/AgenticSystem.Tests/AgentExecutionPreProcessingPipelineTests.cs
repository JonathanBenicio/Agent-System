using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Core.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace AgenticSystem.Tests;

public class AgentExecutionPreProcessingPipelineTests
{
    [Fact]
    public async Task ProcessAsync_ValidatesRequestAndAppliesAgentRules()
    {
        var qualityGateService = Substitute.For<IQualityGateService>();
        var correctionLoop = Substitute.For<ICorrectionLoop>();
        var logger = Substitute.For<ILogger<AgentExecutionPreProcessingPipeline>>();

        qualityGateService.ValidateRequestAsync("hello", Arg.Any<Dictionary<string, object>?>(), Arg.Any<CancellationToken>())
            .Returns(new QualityReport
            {
                OverallPassed = true,
                AverageScore = 10,
                Phase = QualityGatePhase.PreExecution
            });

        var rules = new[]
        {
            new CorrectionRule { Rule = "Sempre citar fonte" },
            new CorrectionRule { Rule = "Responder em português" }
        };

        correctionLoop.GetActiveRulesAsync("user-1", "FinanceAgent")
            .Returns(rules);
        correctionLoop.ApplyRulesToPromptAsync("hello", Arg.Any<IEnumerable<CorrectionRule>>())
            .Returns("enriched hello");

        var sut = new AgentExecutionPreProcessingPipeline(logger, qualityGateService, correctionLoop);

        var result = await sut.ProcessAsync(new AgentExecutionPreProcessingContext
        {
            SessionId = "session-1",
            Input = "hello",
            UserContext = new UserContext { UserId = "user-1" },
            Analysis = new AnalysisResult { Confidence = 1 },
            TargetAgent = "FinanceAgent",
            Metadata = new Dictionary<string, object>
            {
                ["executionMode"] = "direct"
            }
        });

        result.EffectiveInput.Should().Be("enriched hello");
        result.AppliedCorrectionRuleCount.Should().Be(2);
        await qualityGateService.Received(1).ValidateRequestAsync(
            "hello",
            Arg.Is<Dictionary<string, object>?>(metadata => metadata! ["executionMode"].ToString() == "direct"),
            Arg.Any<CancellationToken>());
        await correctionLoop.Received(1).GetActiveRulesAsync("user-1", "FinanceAgent");
    }

    [Fact]
    public async Task ProcessAsync_WhenHostedPathUsesGlobalRules_DoesNotTargetSpecificAgent()
    {
        var qualityGateService = Substitute.For<IQualityGateService>();
        var correctionLoop = Substitute.For<ICorrectionLoop>();
        var logger = Substitute.For<ILogger<AgentExecutionPreProcessingPipeline>>();

        qualityGateService.ValidateRequestAsync("hello", Arg.Any<Dictionary<string, object>?>(), Arg.Any<CancellationToken>())
            .Returns(new QualityReport
            {
                OverallPassed = true,
                AverageScore = 10,
                Phase = QualityGatePhase.PreExecution
            });

        correctionLoop.GetActiveRulesAsync("user-1", null)
            .Returns(Array.Empty<CorrectionRule>());

        var sut = new AgentExecutionPreProcessingPipeline(logger, qualityGateService, correctionLoop);

        var result = await sut.ProcessAsync(new AgentExecutionPreProcessingContext
        {
            SessionId = "session-1",
            Input = "hello",
            UserContext = new UserContext { UserId = "user-1" },
            TargetAgent = null,
            Metadata = new Dictionary<string, object>
            {
                ["executionMode"] = "framework-orchestration"
            }
        });

        result.EffectiveInput.Should().Be("hello");
        result.AppliedCorrectionRuleCount.Should().Be(0);
        await correctionLoop.Received(1).GetActiveRulesAsync("user-1", null);
    }
}