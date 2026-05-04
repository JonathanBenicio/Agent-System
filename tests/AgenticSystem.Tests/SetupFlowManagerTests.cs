using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Core.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace AgenticSystem.Tests;

public class SetupFlowManagerTests
{
    private readonly IUserPreferenceEngine _preferenceEngine;
    private readonly SetupFlowManager _sut;

    public SetupFlowManagerTests()
    {
        _preferenceEngine = Substitute.For<IUserPreferenceEngine>();
        var logger = Substitute.For<ILogger<SetupFlowManager>>();
        _sut = new SetupFlowManager(_preferenceEngine, logger);
    }

    [Fact]
    public void IsSetupRequest_WithSetupIntent_ReturnsTrue()
    {
        _sut.IsSetupRequest("qualquer coisa", IntentType.Setup).Should().BeTrue();
    }

    [Fact]
    public void IsSetupRequest_WithSetupKeyword_ReturnsTrue()
    {
        _sut.IsSetupRequest("quero configurar o sistema", IntentType.Chat).Should().BeTrue();
    }

    [Fact]
    public void IsSetupRequest_WithoutKeyword_ReturnsFalse()
    {
        _sut.IsSetupRequest("me ajude com código", IntentType.Chat).Should().BeFalse();
    }

    [Fact]
    public async Task StartSetupAsync_CreatesWelcomeState()
    {
        var state = await _sut.StartSetupAsync("user1");

        state.UserId.Should().Be("user1");
        state.CurrentStep.Should().Be(SetupStep.Welcome);
        state.IsComplete.Should().BeFalse();
        state.StepNumber.Should().Be(0);
        state.PromptMessage.Should().Contain("Bem-vindo");
    }

    [Fact]
    public async Task IsInSetupFlowAsync_NoFlow_ReturnsFalse()
    {
        var result = await _sut.IsInSetupFlowAsync("unknown");
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsInSetupFlowAsync_ActiveFlow_ReturnsTrue()
    {
        await _sut.StartSetupAsync("user1");
        var result = await _sut.IsInSetupFlowAsync("user1");
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ProcessStepResponseAsync_Welcome_AdvancesToIdentity()
    {
        await _sut.StartSetupAsync("user1");
        var state = await _sut.ProcessStepResponseAsync("user1", "ok");

        state.CurrentStep.Should().Be(SetupStep.Identity);
        state.StepNumber.Should().Be(1);
        state.PromptMessage.Should().Contain("Identidade");
    }

    [Fact]
    public async Task ProcessStepResponseAsync_Identity_ParsesNameAndEmail()
    {
        await _sut.StartSetupAsync("user1");
        await _sut.ProcessStepResponseAsync("user1", "ok"); // Welcome → Identity

        var state = await _sut.ProcessStepResponseAsync("user1", "Alice, alice@empresa.com");

        state.CurrentStep.Should().Be(SetupStep.Preferences);
        state.CollectedData.Should().ContainKey("name");
        state.CollectedData["name"].Should().Be("Alice");
        state.CollectedData["email"].Should().Be("alice@empresa.com");
    }

    [Fact]
    public async Task ProcessStepResponseAsync_Preferences_ParsesStyle()
    {
        await _sut.StartSetupAsync("user1");
        await _sut.ProcessStepResponseAsync("user1", "ok");         // Welcome
        await _sut.ProcessStepResponseAsync("user1", "Alice");       // Identity

        var state = await _sut.ProcessStepResponseAsync("user1", "técnico, pt-br");

        state.CurrentStep.Should().Be(SetupStep.LLMProvider);
        state.CollectedData["style"].Should().Be("Technical");
        state.CollectedData["language"].Should().Be("pt-br");
    }

    [Fact]
    public async Task ProcessStepResponseAsync_CompleteFlow_SetsIsComplete()
    {
        var profile = new UserPreferenceProfile { UserId = "user1", DisplayName = "user1" };
        _preferenceEngine.GetOrCreateProfileAsync("user1", Arg.Any<string>())
            .Returns(profile);
        _preferenceEngine.UpdateProfileAsync(profile).Returns(profile);

        await _sut.StartSetupAsync("user1");
        await _sut.ProcessStepResponseAsync("user1", "ok");                  // Welcome → Identity
        await _sut.ProcessStepResponseAsync("user1", "Alice, a@b.com");      // Identity → Preferences
        await _sut.ProcessStepResponseAsync("user1", "concise, pt-br");      // Preferences → LLMProvider
        await _sut.ProcessStepResponseAsync("user1", "openai");              // LLMProvider → Domains

        var state = await _sut.ProcessStepResponseAsync("user1", "dev, data"); // Domains → Complete

        state.IsComplete.Should().BeTrue();
        state.CurrentStep.Should().Be(SetupStep.Complete);
    }

    [Fact]
    public async Task ProcessStepResponseAsync_NoActiveFlow_StartsNew()
    {
        var state = await _sut.ProcessStepResponseAsync("newuser", "hello");

        state.UserId.Should().Be("newuser");
        state.CurrentStep.Should().Be(SetupStep.Identity);
    }

    [Fact]
    public async Task GetSetupStateAsync_ReturnsCurrentState()
    {
        await _sut.StartSetupAsync("user1");
        var state = await _sut.GetSetupStateAsync("user1");

        state.Should().NotBeNull();
        state!.UserId.Should().Be("user1");
    }

    [Fact]
    public async Task GetSetupStateAsync_NoFlow_ReturnsNull()
    {
        var state = await _sut.GetSetupStateAsync("unknown");
        state.Should().BeNull();
    }

    [Fact]
    public async Task IsInSetupFlowAsync_CompletedFlow_ReturnsFalse()
    {
        var profile = new UserPreferenceProfile { UserId = "user1", DisplayName = "user1" };
        _preferenceEngine.GetOrCreateProfileAsync("user1", Arg.Any<string>())
            .Returns(profile);
        _preferenceEngine.UpdateProfileAsync(profile).Returns(profile);

        await _sut.StartSetupAsync("user1");
        await _sut.ProcessStepResponseAsync("user1", "ok");
        await _sut.ProcessStepResponseAsync("user1", "Alice");
        await _sut.ProcessStepResponseAsync("user1", "concise");
        await _sut.ProcessStepResponseAsync("user1", "openai");
        await _sut.ProcessStepResponseAsync("user1", "dev");

        var inFlow = await _sut.IsInSetupFlowAsync("user1");
        inFlow.Should().BeFalse();
    }
}
