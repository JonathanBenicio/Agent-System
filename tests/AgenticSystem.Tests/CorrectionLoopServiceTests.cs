using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using AgenticSystem.Core.Models;
using AgenticSystem.Core.Services;

namespace AgenticSystem.Tests;

public class CorrectionLoopServiceTests
{
    private readonly CorrectionLoopService _sut;

    public CorrectionLoopServiceTests()
    {
        var logger = Substitute.For<ILogger<CorrectionLoopService>>();
        _sut = new CorrectionLoopService(logger);
    }

    [Fact]
    public async Task AddRuleAsync_CreatesActiveRule()
    {
        var rule = await _sut.AddRuleAsync("user1", "Always use formal tone");

        rule.UserId.Should().Be("user1");
        rule.Rule.Should().Be("Always use formal tone");
        rule.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task GetActiveRulesAsync_ReturnsOnlyActiveForUser()
    {
        await _sut.AddRuleAsync("user1", "Rule 1");
        await _sut.AddRuleAsync("user2", "Rule 2");
        var r3 = await _sut.AddRuleAsync("user1", "Rule 3");
        await _sut.DeactivateRuleAsync(r3.Id);

        var rules = await _sut.GetActiveRulesAsync("user1");

        rules.Should().HaveCount(1);
        rules.First().Rule.Should().Be("Rule 1");
    }

    [Fact]
    public async Task GetActiveRulesAsync_FiltersByAgent()
    {
        await _sut.AddRuleAsync("user1", "Rule for A", targetAgent: "AgentA");
        await _sut.AddRuleAsync("user1", "Rule for B", targetAgent: "AgentB");
        await _sut.AddRuleAsync("user1", "Rule for all");

        var rules = await _sut.GetActiveRulesAsync("user1", "AgentA");

        rules.Should().HaveCount(2); // "Rule for A" + "Rule for all" (no target)
    }

    [Fact]
    public async Task RecordCorrectionAsync_StoresCorrection()
    {
        var correction = await _sut.RecordCorrectionAsync("s1", "original", "corrected", "was wrong");

        correction.OriginalResponse.Should().Be("original");
        correction.CorrectedResponse.Should().Be("corrected");
        correction.Reason.Should().Be("was wrong");
    }

    [Fact]
    public async Task DeactivateRuleAsync_DeactivatesRule()
    {
        var rule = await _sut.AddRuleAsync("user1", "Test rule");

        await _sut.DeactivateRuleAsync(rule.Id);

        var rules = await _sut.GetActiveRulesAsync("user1");
        rules.Should().BeEmpty();
    }

    [Fact]
    public async Task ApplyRulesToPromptAsync_PrependsRules()
    {
        var rule = await _sut.AddRuleAsync("user1", "Be concise");

        var result = await _sut.ApplyRulesToPromptAsync("Hello world", new[] { rule });

        result.Should().Contain("## Human Correction Rules");
        result.Should().Contain("Be concise");
        result.Should().EndWith("Hello world");
    }

    [Fact]
    public async Task ApplyRulesToPromptAsync_NoRules_ReturnsOriginal()
    {
        var result = await _sut.ApplyRulesToPromptAsync("Hello world", Array.Empty<CorrectionRule>());

        result.Should().Be("Hello world");
    }
}
