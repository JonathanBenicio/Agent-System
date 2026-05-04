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
    public void GetProviders_ReturnsOkWithProviderList()
    {
        var providers = new List<LLMProviderInfo>
        {
            new() { Name = "OpenAI", DefaultModel = "gpt-4o", IsEnabled = true, Priority = 1, HasApiKey = true },
            new() { Name = "Claude", DefaultModel = "claude-sonnet-4-20250514", IsEnabled = false, Priority = 3, HasApiKey = false }
        };
        _llmManager.GetAllProviderInfo().Returns(providers);

        var result = _sut.GetProviders();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var data = ok.Value as IEnumerable<LLMProviderInfo>;
        data.Should().HaveCount(2);
    }

    [Fact]
    public void GetProvider_WhenExists_ReturnsOk()
    {
        _llmManager.GetAllProviderInfo().Returns(new List<LLMProviderInfo>
        {
            new() { Name = "OpenAI", DefaultModel = "gpt-4o", IsEnabled = true, Priority = 1 }
        });

        var result = _sut.GetProvider("OpenAI");

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public void GetProvider_WhenNotExists_ReturnsNotFound()
    {
        _llmManager.GetAllProviderInfo().Returns(new List<LLMProviderInfo>());

        var result = _sut.GetProvider("NonExistent");

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public void GetDefaultProvider_WhenAvailable_ReturnsOk()
    {
        var provider = Substitute.For<ILLMProvider>();
        provider.Name.Returns("OpenAI");
        provider.DefaultModel.Returns("gpt-4o");
        provider.IsEnabled.Returns(true);
        provider.Priority.Returns(1);
        _llmManager.GetDefaultProvider().Returns(provider);

        var result = _sut.GetDefaultProvider();

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public void GetDefaultProvider_WhenNoneAvailable_ReturnsNotFound()
    {
        _llmManager.GetDefaultProvider().Returns(x => throw new InvalidOperationException("No providers"));

        var result = _sut.GetDefaultProvider();

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
    public void UpdateProvider_WhenExists_ReturnsOkWithInfo()
    {
        var request = new UpdateProviderRequest { Enabled = false, Priority = 5 };
        _llmManager.UpdateProvider("OpenAI", request).Returns(true);
        _llmManager.GetAllProviderInfo().Returns(new List<LLMProviderInfo>
        {
            new() { Name = "OpenAI", DefaultModel = "gpt-4o", IsEnabled = false, Priority = 5 }
        });

        var result = _sut.UpdateProvider("OpenAI", request);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var info = ok.Value as LLMProviderInfo;
        info.Should().NotBeNull();
        info!.Name.Should().Be("OpenAI");
    }

    [Fact]
    public void UpdateProvider_WhenNotExists_ReturnsNotFound()
    {
        var request = new UpdateProviderRequest { Enabled = true };
        _llmManager.UpdateProvider("NonExistent", request).Returns(false);

        var result = _sut.UpdateProvider("NonExistent", request);

        result.Should().BeOfType<NotFoundObjectResult>();
    }
}
