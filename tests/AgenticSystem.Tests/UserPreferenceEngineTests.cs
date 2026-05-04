using AgenticSystem.Core.Models;
using AgenticSystem.Core.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace AgenticSystem.Tests;

public class UserPreferenceEngineTests
{
    private readonly UserPreferenceEngine _sut;

    public UserPreferenceEngineTests()
    {
        var logger = Substitute.For<ILogger<UserPreferenceEngine>>();
        _sut = new UserPreferenceEngine(logger);
    }

    [Fact]
    public async Task GetOrCreateProfileAsync_NewUser_CreatesProfile()
    {
        var profile = await _sut.GetOrCreateProfileAsync("user1", "Alice");

        profile.UserId.Should().Be("user1");
        profile.DisplayName.Should().Be("Alice");
        profile.IsActive.Should().BeTrue();
        profile.TotalInteractions.Should().Be(0);
    }

    [Fact]
    public async Task GetOrCreateProfileAsync_ExistingUser_ReturnsSameProfile()
    {
        await _sut.GetOrCreateProfileAsync("user1", "Alice");
        var profile = await _sut.GetOrCreateProfileAsync("user1");

        profile.DisplayName.Should().Be("Alice");
    }

    [Fact]
    public async Task GetOrCreateProfileAsync_UpdatesDisplayName()
    {
        await _sut.GetOrCreateProfileAsync("user1", "Alice");
        var profile = await _sut.GetOrCreateProfileAsync("user1", "Alice Updated");

        profile.DisplayName.Should().Be("Alice Updated");
    }

    [Fact]
    public async Task UpdateProfileAsync_SavesChanges()
    {
        var profile = await _sut.GetOrCreateProfileAsync("user1", "Alice");
        profile.RiskTolerance = RiskTolerance.Aggressive;
        profile.ResponsePreferences.Style = CommunicationStyle.Technical;

        var updated = await _sut.UpdateProfileAsync(profile);

        updated.RiskTolerance.Should().Be(RiskTolerance.Aggressive);
        updated.ResponsePreferences.Style.Should().Be(CommunicationStyle.Technical);
    }

    [Fact]
    public async Task PersonalizePromptAsync_UnknownUser_ReturnsOriginalPrompt()
    {
        var adjustment = await _sut.PersonalizePromptAsync("unknown", "test prompt");

        adjustment.OriginalPrompt.Should().Be("test prompt");
        adjustment.AdjustedPrompt.Should().Be("test prompt");
        adjustment.AppliedRiskLevel.Should().Be(RiskTolerance.Moderate);
    }

    [Fact]
    public async Task PersonalizePromptAsync_ConciseStyle_AddsDirective()
    {
        var profile = await _sut.GetOrCreateProfileAsync("user1");
        profile.ResponsePreferences.Style = CommunicationStyle.Concise;
        await _sut.UpdateProfileAsync(profile);

        var adjustment = await _sut.PersonalizePromptAsync("user1", "explain microservices");

        adjustment.AdjustedPrompt.Should().Contain("[Be concise and direct]");
        adjustment.AppliedPreferences.Should().Contain("style:Concise");
    }

    [Fact]
    public async Task PersonalizePromptAsync_ConservativeRisk_AddsWarning()
    {
        var profile = await _sut.GetOrCreateProfileAsync("user1");
        profile.RiskTolerance = RiskTolerance.Conservative;
        await _sut.UpdateProfileAsync(profile);

        var adjustment = await _sut.PersonalizePromptAsync("user1", "suggest an approach");

        adjustment.AdjustedPrompt.Should().Contain("safe, well-tested");
        adjustment.AppliedRiskLevel.Should().Be(RiskTolerance.Conservative);
    }

    [Fact]
    public async Task PersonalizePromptAsync_NoCodeExamples_AddsSkipDirective()
    {
        var profile = await _sut.GetOrCreateProfileAsync("user1");
        profile.ResponsePreferences.IncludeCodeExamples = false;
        await _sut.UpdateProfileAsync(profile);

        var adjustment = await _sut.PersonalizePromptAsync("user1", "explain DI");

        adjustment.AdjustedPrompt.Should().Contain("Skip code examples");
    }

    [Fact]
    public async Task PersonalizePromptAsync_LowMaxTokens_AddsTokenLimit()
    {
        var profile = await _sut.GetOrCreateProfileAsync("user1");
        profile.ResponsePreferences.MaxResponseTokens = 500;
        await _sut.UpdateProfileAsync(profile);

        var adjustment = await _sut.PersonalizePromptAsync("user1", "explain DI");

        adjustment.AdjustedPrompt.Should().Contain("500 tokens");
    }

    [Fact]
    public async Task RecordInteractionAsync_UpdatesSatisfaction()
    {
        await _sut.GetOrCreateProfileAsync("user1");
        await _sut.RecordInteractionAsync("user1", "CodeAgent", 0.9);

        var profile = await _sut.GetOrCreateProfileAsync("user1");

        profile.TotalInteractions.Should().Be(1);
        profile.AgentSatisfactionScores.Should().ContainKey("CodeAgent");
        profile.AgentSatisfactionScores["CodeAgent"].Should().Be(0.9);
    }

    [Fact]
    public async Task RecordInteractionAsync_MultipleInteractions_UsesEMA()
    {
        await _sut.GetOrCreateProfileAsync("user1");

        await _sut.RecordInteractionAsync("user1", "CodeAgent", 1.0);
        await _sut.RecordInteractionAsync("user1", "CodeAgent", 0.0);

        var profile = await _sut.GetOrCreateProfileAsync("user1");

        // EMA: first = 1.0, second = 1.0 * 0.7 + 0.0 * 0.3 = 0.7
        profile.AgentSatisfactionScores["CodeAgent"].Should().BeApproximately(0.7, 0.01);
    }

    [Fact]
    public async Task RecordInteractionAsync_UnknownUser_DoesNotThrow()
    {
        var act = () => _sut.RecordInteractionAsync("unknown", "Agent", 0.5);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task RecommendAgentAsync_WithPreferredAgents_ReturnsFirst()
    {
        var profile = await _sut.GetOrCreateProfileAsync("user1");
        profile.PreferredAgents.Add("FavoriteAgent");
        await _sut.UpdateProfileAsync(profile);

        var analysis = new AnalysisResult { PrimaryDomain = "general" };
        var recommended = await _sut.RecommendAgentAsync("user1", analysis);

        recommended.Should().Be("FavoriteAgent");
    }

    [Fact]
    public async Task RecommendAgentAsync_WithSatisfactionScores_ReturnsTopAgent()
    {
        await _sut.GetOrCreateProfileAsync("user1");
        await _sut.RecordInteractionAsync("user1", "AgentA", 0.5);
        await _sut.RecordInteractionAsync("user1", "AgentB", 0.9);

        var analysis = new AnalysisResult { PrimaryDomain = "general" };
        var recommended = await _sut.RecommendAgentAsync("user1", analysis);

        recommended.Should().Be("AgentB");
    }

    [Fact]
    public async Task RecommendAgentAsync_UnknownUser_ReturnsNull()
    {
        var analysis = new AnalysisResult { PrimaryDomain = "general" };
        var recommended = await _sut.RecommendAgentAsync("unknown", analysis);

        recommended.Should().BeNull();
    }

    [Fact]
    public async Task DeactivateProfileAsync_SetsInactive()
    {
        await _sut.GetOrCreateProfileAsync("user1");
        await _sut.DeactivateProfileAsync("user1");

        var profile = await _sut.GetOrCreateProfileAsync("user1");
        profile.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task DeactivateProfileAsync_UnknownUser_DoesNotThrow()
    {
        var act = () => _sut.DeactivateProfileAsync("unknown");
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task PersonalizePromptAsync_RecommendsAgentFromSatisfaction()
    {
        await _sut.GetOrCreateProfileAsync("user1");
        await _sut.RecordInteractionAsync("user1", "BestAgent", 0.95);
        await _sut.RecordInteractionAsync("user1", "OkAgent", 0.5);

        var adjustment = await _sut.PersonalizePromptAsync("user1", "help me");

        adjustment.RecommendedAgent.Should().Be("BestAgent");
    }
}
