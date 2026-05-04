using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using AgenticSystem.Core.Models;
using AgenticSystem.Core.Services;

namespace AgenticSystem.Tests;

public class ReflectionEngineTests
{
    private readonly ReflectionEngine _sut;

    public ReflectionEngineTests()
    {
        var logger = Substitute.For<ILogger<ReflectionEngine>>();
        _sut = new ReflectionEngine(logger);
    }

    [Fact]
    public async Task ReflectAsync_HighConfidence_ReturnsInfoSeverity()
    {
        var reflection = await _sut.ReflectAsync("session1", "Agent1", "analyzed data", "success", 0.9);

        reflection.SessionId.Should().Be("session1");
        reflection.AgentName.Should().Be("Agent1");
        reflection.ConfidenceInOutcome.Should().Be(0.9);
        reflection.Severity.Should().Be(ReflectionSeverity.Info);
        reflection.LessonsLearned.Should().Contain(l => l.Contains("High confidence"));
    }

    [Fact]
    public async Task ReflectAsync_LowConfidence_ReturnsWarning()
    {
        var reflection = await _sut.ReflectAsync("session1", "Agent1", "tried", "unclear", 0.4);

        reflection.Severity.Should().Be(ReflectionSeverity.Warning);
        reflection.Deviations.Should().Contain(d => d.Contains("Low confidence"));
    }

    [Fact]
    public async Task ReflectAsync_VeryLowConfidence_ReturnsCritical()
    {
        var reflection = await _sut.ReflectAsync("session1", "Agent1", "guessed", "wrong", 0.2);

        reflection.Severity.Should().Be(ReflectionSeverity.Critical);
        reflection.ImprovementSuggestion.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task GetSessionReflectionsAsync_ReturnsOnlyMatchingSession()
    {
        await _sut.ReflectAsync("s1", "A1", "a", "b", 0.8);
        await _sut.ReflectAsync("s2", "A1", "c", "d", 0.8);
        await _sut.ReflectAsync("s1", "A2", "e", "f", 0.5);

        var reflections = await _sut.GetSessionReflectionsAsync("s1");

        reflections.Should().HaveCount(2);
        reflections.Should().AllSatisfy(r => r.SessionId.Should().Be("s1"));
    }

    [Fact]
    public async Task GetRecentLearningsAsync_ReturnsOnlyWithLessons()
    {
        await _sut.ReflectAsync("s1", "A1", "action", "result", 0.9); // has lessons (high conf)
        await _sut.ReflectAsync("s1", "A1", "action", "short", 0.6);  // may not have lessons

        var learnings = await _sut.GetRecentLearningsAsync(5);

        learnings.Should().AllSatisfy(r => r.LessonsLearned.Should().NotBeEmpty());
    }
}
