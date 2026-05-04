using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using AgenticSystem.Core.LLM.Interfaces;
using AgenticSystem.Infrastructure.LLM;

namespace AgenticSystem.Tests;

public class LLMManagerUpdateTests
{
    private readonly ILLMProvider _openAi;
    private readonly ILLMProvider _claude;
    private readonly LLMManager _sut;

    public LLMManagerUpdateTests()
    {
        _openAi = Substitute.For<ILLMProvider>();
        _openAi.Name.Returns("OpenAI");
        _openAi.IsEnabled.Returns(true);
        _openAi.Priority.Returns(1);
        _openAi.DefaultModel.Returns("gpt-4o");

        _claude = Substitute.For<ILLMProvider>();
        _claude.Name.Returns("Claude");
        _claude.IsEnabled.Returns(false);
        _claude.Priority.Returns(3);
        _claude.DefaultModel.Returns("claude-sonnet-4-20250514");

        var logger = Substitute.For<ILogger<LLMManager>>();
        _sut = new LLMManager(new[] { _openAi, _claude }, logger);
    }

    [Fact]
    public void UpdateProvider_WhenExists_CallsConfigureAndReturnsTrue()
    {
        var request = new UpdateProviderRequest { Enabled = false, Priority = 5 };

        var result = _sut.UpdateProvider("OpenAI", request);

        result.Should().BeTrue();
        _openAi.Received(1).Configure(null, null, false, 5);
    }

    [Fact]
    public void UpdateProvider_WhenNotExists_ReturnsFalse()
    {
        var request = new UpdateProviderRequest { Enabled = true };

        var result = _sut.UpdateProvider("NonExistent", request);

        result.Should().BeFalse();
    }

    [Fact]
    public void UpdateProvider_IsCaseInsensitive()
    {
        var request = new UpdateProviderRequest { Priority = 2 };

        var result = _sut.UpdateProvider("openai", request);

        result.Should().BeTrue();
        _openAi.Received(1).Configure(null, null, null, 2);
    }

    [Fact]
    public void UpdateProvider_WhenDisabled_RemovesFromEnabledProviders()
    {
        // After Configure, IsEnabled will return false
        _openAi.IsEnabled.Returns(false);

        var request = new UpdateProviderRequest { Enabled = false };
        _sut.UpdateProvider("OpenAI", request);

        _sut.GetEnabledProviders().Should().NotContain(p => p.Name == "OpenAI");
    }

    [Fact]
    public void UpdateProvider_WhenEnabled_AddsToEnabledProviders()
    {
        // After Configure, IsEnabled will return true
        _claude.IsEnabled.Returns(true);

        var request = new UpdateProviderRequest { Enabled = true };
        _sut.UpdateProvider("Claude", request);

        _sut.GetEnabledProviders().Should().Contain(p => p.Name == "Claude");
    }

    [Fact]
    public void UpdateProvider_UpdatesApiKeyAndModel()
    {
        var request = new UpdateProviderRequest { ApiKey = "sk-new", DefaultModel = "gpt-4-turbo" };

        _sut.UpdateProvider("OpenAI", request);

        _openAi.Received(1).Configure("sk-new", "gpt-4-turbo", null, null);
    }
}
