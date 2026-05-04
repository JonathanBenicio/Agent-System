using AgenticSystem.Core.LLM.Models;
using AgenticSystem.Infrastructure.LLM.Adapters;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using MEAIChatMessage = Microsoft.Extensions.AI.ChatMessage;
using MEAIChatResponse = Microsoft.Extensions.AI.ChatResponse;

namespace AgenticSystem.Tests;

public class ChatClientProviderAdapterTests
{
    private readonly IChatClient _chatClient;
    private readonly ILogger<ChatClientProviderAdapter> _logger;
    private readonly ChatClientProviderAdapter _sut;

    public ChatClientProviderAdapterTests()
    {
        _chatClient = Substitute.For<IChatClient>();
        _logger = Substitute.For<ILogger<ChatClientProviderAdapter>>();
        _sut = new ChatClientProviderAdapter(_chatClient, _logger);
    }

    [Fact]
    public void Name_ReturnsDefault()
    {
        _sut.Name.Should().Be("M.E.AI");
    }

    [Fact]
    public void DefaultModel_ReturnsConfigured()
    {
        _sut.DefaultModel.Should().Be("gpt-4o");
    }

    [Fact]
    public void IsEnabled_DefaultTrue()
    {
        _sut.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void Configure_UpdatesValues()
    {
        _sut.Configure(null, "gpt-3.5", false, 5);

        _sut.DefaultModel.Should().Be("gpt-3.5");
        _sut.IsEnabled.Should().BeFalse();
        _sut.Priority.Should().Be(5);
    }

    [Fact]
    public async Task GenerateAsync_WithPrompt_ReturnsContent()
    {
        var expectedText = "Hello from M.E.AI";
        var chatResponse = new MEAIChatResponse(new MEAIChatMessage(ChatRole.Assistant, expectedText));

        _chatClient
            .GetResponseAsync(Arg.Any<IList<MEAIChatMessage>>(), Arg.Any<ChatOptions?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(chatResponse));

        var request = new LLMRequest { Prompt = "Hi" };
        var result = await _sut.GenerateAsync(request);

        result.Success.Should().BeTrue();
        result.Content.Should().Be(expectedText);
        result.Provider.Should().Be("M.E.AI");
    }

    [Fact]
    public async Task GenerateAsync_WithMessages_MapsRolesCorrectly()
    {
        var chatResponse = new MEAIChatResponse(new MEAIChatMessage(ChatRole.Assistant, "ok"));

        _chatClient
            .GetResponseAsync(Arg.Any<IList<MEAIChatMessage>>(), Arg.Any<ChatOptions?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(chatResponse));

        var request = new LLMRequest
        {
            SystemPrompt = "You are helpful.",
            Messages = new List<Core.LLM.Models.ChatMessage>
            {
                Core.LLM.Models.ChatMessage.User("Hello"),
                Core.LLM.Models.ChatMessage.Assistant("Hi!"),
                Core.LLM.Models.ChatMessage.User("How are you?")
            }
        };

        var result = await _sut.GenerateAsync(request);
        result.Success.Should().BeTrue();

        await _chatClient.Received(1)
            .GetResponseAsync(
                Arg.Is<IList<MEAIChatMessage>>(msgs => msgs.Count == 4), // system + 3 messages
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateAsync_OnException_ReturnsFail()
    {
        _chatClient
            .GetResponseAsync(Arg.Any<IList<MEAIChatMessage>>(), Arg.Any<ChatOptions?>(), Arg.Any<CancellationToken>())
            .Returns<MEAIChatResponse>(_ => throw new HttpRequestException("Connection refused"));

        var request = new LLMRequest { Prompt = "Hi" };
        var result = await _sut.GenerateAsync(request);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Connection refused");
    }

    [Fact]
    public async Task IsAvailableAsync_WhenDisabled_ReturnsFalse()
    {
        var adapter = new ChatClientProviderAdapter(_chatClient, _logger, enabled: false);
        (await adapter.IsAvailableAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task IsAvailableAsync_WhenPingSucceeds_ReturnsTrue()
    {
        var chatResponse = new MEAIChatResponse(new MEAIChatMessage(ChatRole.Assistant, "pong"));

        _chatClient
            .GetResponseAsync(Arg.Any<IList<MEAIChatMessage>>(), Arg.Any<ChatOptions?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(chatResponse));

        (await _sut.IsAvailableAsync()).Should().BeTrue();
    }

    [Fact]
    public async Task IsAvailableAsync_WhenPingFails_ReturnsFalse()
    {
        _chatClient
            .GetResponseAsync(Arg.Any<IList<MEAIChatMessage>>(), Arg.Any<ChatOptions?>(), Arg.Any<CancellationToken>())
            .Returns<MEAIChatResponse>(_ => throw new Exception("down"));

        (await _sut.IsAvailableAsync()).Should().BeFalse();
    }
}
