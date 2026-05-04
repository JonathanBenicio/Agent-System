using AgenticSystem.Api.Controllers;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Tests;

public class VoiceControllerTests
{
    private readonly IMetaAgent _metaAgent;
    private readonly ILogger<VoiceController> _logger;
    private readonly VoiceController _sut;

    public VoiceControllerTests()
    {
        _metaAgent = Substitute.For<IMetaAgent>();
        _logger = Substitute.For<ILogger<VoiceController>>();
        _sut = new VoiceController(_metaAgent, _logger);
        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
    }

    [Fact]
    public async Task Ask_WithValidText_ReturnsOkWithCleanText()
    {
        _metaAgent
            .ProcessRequestAsync(Arg.Any<string>(), Arg.Any<UserContext>())
            .Returns(new AgentResponse { Content = "**Hello** world!" });

        var request = new VoiceRequest("Que horas são?");
        var result = await _sut.Ask(request);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = ok.Value.Should().BeOfType<VoiceResponse>().Subject;
        response.Success.Should().BeTrue();
        response.Text.Should().NotContain("**");
        response.Text.Should().Contain("Hello");
    }

    [Fact]
    public async Task Ask_WithEmptyText_ReturnsBadRequest()
    {
        var request = new VoiceRequest("");
        var result = await _sut.Ask(request);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Ask_WithWhitespaceText_ReturnsBadRequest()
    {
        var request = new VoiceRequest("   ");
        var result = await _sut.Ask(request);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Theory]
    [InlineData("# Title\nContent", "Title\nContent")]
    [InlineData("**bold** text", "bold text")]
    [InlineData("`code` here", "code here")]
    [InlineData("[link](http://example.com)", "link")]
    [InlineData("![alt](img.png)", "")]
    [InlineData("---", "")]
    [InlineData("- item1\n- item2", "item1\nitem2")]
    public void StripMarkdown_RemovesFormatting(string input, string expected)
    {
        VoiceController.StripMarkdown(input).Should().Be(expected);
    }

    [Fact]
    public void StripMarkdown_RemovesCodeBlocks()
    {
        var input = "Before\n```csharp\nvar x = 1;\n```\nAfter";
        var result = VoiceController.StripMarkdown(input);

        result.Should().NotContain("```");
        result.Should().Contain("var x = 1;");
    }

    [Fact]
    public async Task Ask_SetsDefaultUserIdAndName()
    {
        _metaAgent
            .ProcessRequestAsync(Arg.Any<string>(), Arg.Any<UserContext>())
            .Returns(new AgentResponse { Content = "ok" });

        var request = new VoiceRequest("test");
        await _sut.Ask(request);

        await _metaAgent.Received(1).ProcessRequestAsync(
            "test",
            Arg.Is<UserContext>(c => c.UserId == "voice-user" && c.Name == "Voice User"));
    }

    [Fact]
    public async Task Ask_WithCustomUserId_PassesIt()
    {
        _metaAgent
            .ProcessRequestAsync(Arg.Any<string>(), Arg.Any<UserContext>())
            .Returns(new AgentResponse { Content = "ok" });

        var request = new VoiceRequest("test", UserId: "alexa-123", UserName: "Echo");
        await _sut.Ask(request);

        await _metaAgent.Received(1).ProcessRequestAsync(
            "test",
            Arg.Is<UserContext>(c => c.UserId == "alexa-123" && c.Name == "Echo"));
    }
}
