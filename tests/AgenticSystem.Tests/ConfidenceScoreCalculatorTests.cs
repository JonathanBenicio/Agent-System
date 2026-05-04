using FluentAssertions;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Core.Services;

namespace AgenticSystem.Tests;

public class ConfidenceScoreCalculatorTests
{
    private readonly ConfidenceScoreCalculator _sut = new();

    [Fact]
    public void Calculate_SuccessfulResponse_ReturnsScore()
    {
        var response = AgentResponse.Ok("test content", "TestAgent", AgentTier.Specialist);

        var score = _sut.Calculate(response);

        score.Value.Should().BeGreaterThan(0);
        score.Level.Should().NotBe(ConfidenceLevel.RequiresHumanReview);
        score.Factors.Should().NotBeEmpty();
    }

    [Fact]
    public void Calculate_FailedResponse_LowScore()
    {
        var response = AgentResponse.Error("something failed");

        var score = _sut.Calculate(response);

        score.Value.Should().BeLessThan(0.7);
    }

    [Fact]
    public void Calculate_WithRAGContext_IncludesRAGFactor()
    {
        var response = AgentResponse.Ok("answer", "Agent1", AgentTier.Specialist);
        var rag = new RAGContext
        {
            Chunks = new List<RankedChunk>
            {
                new() { Content = "relevant", ReRankedScore = 0.95 },
                new() { Content = "also relevant", ReRankedScore = 0.85 }
            }
        };

        var score = _sut.Calculate(response, rag);

        score.Factors.Should().Contain(f => f.Contains("RAG"));
    }

    [Fact]
    public void Calculate_WithToolsUsed_HigherScore()
    {
        var withTools = AgentResponse.Ok("answer", "Agent1", AgentTier.Specialist);
        withTools.ToolsUsed = new List<string> { "search", "calculate" };

        var withoutTools = AgentResponse.Ok("answer", "Agent1", AgentTier.Specialist);

        var scoreWithTools = _sut.Calculate(withTools);
        var scoreWithoutTools = _sut.Calculate(withoutTools);

        scoreWithTools.Value.Should().BeGreaterThan(scoreWithoutTools.Value);
    }

    [Fact]
    public void Calculate_WithReflections_IncludesReflectionFactor()
    {
        var response = AgentResponse.Ok("answer", "Agent1", AgentTier.Specialist);
        var reflections = new List<Reflection>
        {
            new() { ConfidenceInOutcome = 0.8, SessionId = "s1", AgentName = "A1", ActionTaken = "a", Outcome = "b" },
            new() { ConfidenceInOutcome = 0.9, SessionId = "s1", AgentName = "A1", ActionTaken = "c", Outcome = "d" }
        };

        var score = _sut.Calculate(response, reflections: reflections);

        score.Factors.Should().Contain(f => f.Contains("reflexão"));
    }

    [Fact]
    public void Calculate_HighConfidence_MapsToHighLevel()
    {
        var response = AgentResponse.Ok("great answer", "Agent1", AgentTier.Specialist);
        response.ToolsUsed = new List<string> { "search" };

        var rag = new RAGContext
        {
            Chunks = new List<RankedChunk>
            {
                new() { Content = "highly relevant", ReRankedScore = 0.95 }
            }
        };

        var score = _sut.Calculate(response, rag);

        score.Level.Should().Be(ConfidenceLevel.High);
        score.Label.Should().Contain("Alta");
    }

    [Fact]
    public void Calculate_RequiresConfirmation_ForLowScores()
    {
        var response = AgentResponse.Error("failed");

        var score = _sut.Calculate(response);

        score.Level.Should().NotBe(ConfidenceLevel.High);
    }

    // ═══════════════════════════════════════════════════════════
    // ML20 — Tool Availability Impact
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public void Calculate_NoCoverage_ScoreBelowRefusalThreshold()
    {
        var response = AgentResponse.Error("no tools available");
        var toolResult = ToolAvailabilityResult.NoCoverage(new[] { "finance-api", "calendar" });

        var score = _sut.Calculate(response, ragContext: null, reflections: null, toolAvailability: toolResult);

        // 0% coverage + error response should produce score < 0.3
        score.Value.Should().BeLessThan(0.3);
        score.Level.Should().BeOneOf(ConfidenceLevel.Low, ConfidenceLevel.RequiresHumanReview);
        score.RequiresConfirmation.Should().BeTrue();
    }

    [Fact]
    public void Calculate_FullCoverage_DoesNotPenalize()
    {
        var response = AgentResponse.Ok("result", "Agent1", AgentTier.Specialist);
        response.ToolsUsed = new List<string> { "search" };
        var toolResult = ToolAvailabilityResult.FullCoverage(new[] { "search" });

        var score = _sut.Calculate(response, ragContext: null, reflections: null, toolAvailability: toolResult);

        score.Value.Should().BeGreaterOrEqualTo(0.7);
        score.Level.Should().Be(ConfidenceLevel.High);
    }

    [Fact]
    public void Calculate_PartialCoverage_MediumScore()
    {
        var response = AgentResponse.Ok("partial", "Agent1", AgentTier.Specialist);
        var toolResult = new ToolAvailabilityResult
        {
            RequiredCount = 4,
            AvailableTools = new[] { "search", "github" },
            MissingTools = new[] { "finance-api", "calendar" }
        };

        var score = _sut.Calculate(response, ragContext: null, reflections: null, toolAvailability: toolResult);

        // 50% coverage → factor ≈ 0.5. Combined with success=1.0, no RAG=0.3, no tools=0.5
        score.Value.Should().BeInRange(0.3, 0.7);
        score.Level.Should().Be(ConfidenceLevel.Medium);
    }

    [Fact]
    public void Calculate_WithToolAvailability_IncludesToolFactor()
    {
        var response = AgentResponse.Ok("answer", "Agent1", AgentTier.Specialist);
        var toolResult = ToolAvailabilityResult.NoCoverage(new[] { "finance-api" });

        var score = _sut.Calculate(response, ragContext: null, reflections: null, toolAvailability: toolResult);

        score.Factors.Should().Contain(f => f.Contains("tool"));
    }

    [Fact]
    public void Calculate_NullToolAvailability_NoExtraFactor()
    {
        var response = AgentResponse.Ok("answer", "Agent1", AgentTier.Specialist);

        var scoreWith = _sut.Calculate(response, ragContext: null, reflections: null, toolAvailability: null);
        var scoreWithout = _sut.Calculate(response);

        scoreWith.Value.Should().Be(scoreWithout.Value);
    }
}
