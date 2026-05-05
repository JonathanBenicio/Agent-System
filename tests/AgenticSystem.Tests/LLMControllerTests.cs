using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NSubstitute;
using AgenticSystem.Api.Controllers;
using AgenticSystem.Core.LLM.Interfaces;

namespace AgenticSystem.Tests;

public class LLMControllerTests
{
    private readonly ILLMManager _llmManager;
    private readonly LLMController _sut;

    public LLMControllerTests()
    {
        _llmManager = Substitute.For<ILLMManager>();
        var logger = Substitute.For<ILogger<LLMController>>();
        _sut = new LLMController(_llmManager, logger);
    }

    [Fact]
    public async Task GetProviders_ReturnsOkWithProviderList()
    {
        var providers = new List<LLMProviderInfo>
        {
            new() { Name = "OpenAI", DefaultModel = "gpt-4o", IsEnabled = true, Priority = 1, HasApiKey = true },
            new() { Name = "Claude", DefaultModel = "claude-sonnet-4-20250514", IsEnabled = false, Priority = 3, HasApiKey = false }
        };
        _llmManager.GetConfigurationAsync(Arg.Any<CancellationToken>()).Returns(new LLMConfigurationInfo
        {
            DefaultProvider = "OpenAI",
            DefaultModel = "gpt-4o",
            Providers = providers
        });

        var result = await _sut.GetProviders(CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var data = ok.Value as IEnumerable<LLMProviderInfo>;
        data.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetProvider_WhenExists_ReturnsOk()
    {
        _llmManager.GetConfigurationAsync(Arg.Any<CancellationToken>()).Returns(new LLMConfigurationInfo
        {
            DefaultProvider = "OpenAI",
            DefaultModel = "gpt-4o",
            Providers = new List<LLMProviderInfo>
            {
                new() { Name = "OpenAI", DefaultModel = "gpt-4o", IsEnabled = true, Priority = 1 }
            }
        });

        var result = await _sut.GetProvider("OpenAI", CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetProvider_WhenNotExists_ReturnsNotFound()
    {
        _llmManager.GetConfigurationAsync(Arg.Any<CancellationToken>()).Returns(new LLMConfigurationInfo
        {
            DefaultProvider = "OpenAI",
            DefaultModel = "gpt-4o",
            Providers = new List<LLMProviderInfo>()
        });

        var result = await _sut.GetProvider("NonExistent", CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetDefaultProvider_WhenAvailable_ReturnsOk()
    {
        _llmManager.GetConfigurationAsync(Arg.Any<CancellationToken>()).Returns(new LLMConfigurationInfo
        {
            DefaultProvider = "OpenAI",
            DefaultModel = "gpt-4o",
            Providers = new List<LLMProviderInfo>
            {
                new() { Name = "OpenAI", DefaultModel = "gpt-4o", IsEnabled = true, Priority = 1 }
            }
        });

        var result = await _sut.GetDefaultProvider(CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetDefaultProvider_WhenNoneAvailable_ReturnsNotFound()
    {
        _llmManager.GetConfigurationAsync(Arg.Any<CancellationToken>()).Returns(new LLMConfigurationInfo
        {
            DefaultProvider = "OpenAI",
            DefaultModel = "gpt-4o",
            Providers = new List<LLMProviderInfo>()
        });

        var result = await _sut.GetDefaultProvider(CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task TestProvider_ReturnsAvailability()
    {
        _llmManager.TestProviderAsync("OpenAI", Arg.Any<CancellationToken>()).Returns(true);

        var result = await _sut.TestProvider("OpenAI", CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task UpdateProvider_WhenExists_ReturnsOkWithInfo()
    {
        var request = new UpdateProviderRequest { Enabled = false, Priority = 5 };
        _llmManager.UpdateProviderAsync("OpenAI", request, Arg.Any<CancellationToken>()).Returns(true);
        _llmManager.GetConfigurationAsync(Arg.Any<CancellationToken>()).Returns(new LLMConfigurationInfo
        {
            DefaultProvider = "OpenAI",
            DefaultModel = "gpt-4o",
            Providers = new List<LLMProviderInfo>
            {
                new() { Name = "OpenAI", DefaultModel = "gpt-4o", IsEnabled = false, Priority = 5 }
            }
        });

        var result = await _sut.UpdateProvider("OpenAI", request, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var info = ok.Value as LLMProviderInfo;
        info.Should().NotBeNull();
        info!.Name.Should().Be("OpenAI");
    }

    [Fact]
    public async Task UpdateProvider_WhenNotExists_ReturnsNotFound()
    {
        var request = new UpdateProviderRequest { Enabled = true };
        _llmManager.UpdateProviderAsync("NonExistent", request, Arg.Any<CancellationToken>()).Returns(false);

        var result = await _sut.UpdateProvider("NonExistent", request, CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }
}
