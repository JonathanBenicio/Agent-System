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
    public async Task UpdateProvider_WhenExists_CallsConfigureAndReturnsTrue()
    {
        var request = new UpdateProviderRequest { Enabled = false, Priority = 5 };

        var result = await _sut.UpdateProviderAsync("OpenAI", request);

        result.Should().NotBeNull();
        _openAi.Received(1).Configure(null, null, false, 5);
    }

    [Fact]
    public async Task UpdateProvider_WhenNotExists_ReturnsFalse()
    {
        var request = new UpdateProviderRequest { Enabled = true };

        var result = await _sut.UpdateProviderAsync("NonExistent", request);

        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateProvider_IsCaseInsensitive()
    {
        var request = new UpdateProviderRequest { Priority = 2 };

        var result = await _sut.UpdateProviderAsync("openai", request);

        result.Should().NotBeNull();
        _openAi.Received(1).Configure(null, null, null, 2);
    }

    [Fact]
    public async Task UpdateProvider_WhenDisabled_RemovesFromEnabledProviders()
    {
        // After Configure, IsEnabled will return false
        _openAi.IsEnabled.Returns(false);

        var request = new UpdateProviderRequest { Enabled = false };
        await _sut.UpdateProviderAsync("OpenAI", request);

        (await _sut.GetEnabledProvidersAsync()).Should().NotContain(p => p.Name == "OpenAI");
    }

    [Fact]
    public async Task UpdateProvider_WhenEnabled_AddsToEnabledProviders()
    {
        // After Configure, IsEnabled will return true
        _claude.IsEnabled.Returns(true);

        var request = new UpdateProviderRequest { Enabled = true };
        await _sut.UpdateProviderAsync("Claude", request);

        (await _sut.GetEnabledProvidersAsync()).Should().Contain(p => p.Name == "Claude");
    }

    [Fact]
    public async Task UpdateProvider_UpdatesApiKeyAndModel()
    {
        var request = new UpdateProviderRequest { ApiKey = "sk-new", DefaultModel = "gpt-4-turbo" };

        await _sut.UpdateProviderAsync("OpenAI", request);

        _openAi.Received(1).Configure("sk-new", "gpt-4-turbo", null, null);
    }
}
