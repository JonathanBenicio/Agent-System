using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Core.Services;
using System.Threading;
using FluentAssertions;
using AIChatResponse = Microsoft.Extensions.AI.ChatResponse;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace AgenticSystem.Tests;

public class DynamicAgentServiceTests
{
    private readonly IAgentFactory _agentFactory;
    private readonly IChatClient _chatClient;
    private readonly DynamicAgentService _sut;

    public DynamicAgentServiceTests()
    {
        _agentFactory = Substitute.For<IAgentFactory>();
        _chatClient = Substitute.For<IChatClient>();
        var logger = Substitute.For<ILogger<DynamicAgentService>>();
        _sut = new DynamicAgentService(_agentFactory, _chatClient, logger);
    }

    [Fact]
    public async Task IsAgentCreationRequestAsync_WithCreateAgentIntent_ReturnsTrue()
    {
        var analysis = new AnalysisResult { Intent = IntentType.CreateAgent };

        var result = await _sut.IsAgentCreationRequestAsync("qualquer coisa", analysis);

        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("crie um agente de finanças")]
    [InlineData("criar agente para vendas")]
    [InlineData("create agent for devops")]
    [InlineData("quero um assistente de saúde")]
    public async Task IsAgentCreationRequestAsync_WithKeyword_ReturnsTrue(string input)
    {
        var analysis = new AnalysisResult { Intent = IntentType.Chat };

        var result = await _sut.IsAgentCreationRequestAsync(input, analysis);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsAgentCreationRequestAsync_NoMatchingKeyword_ReturnsFalse()
    {
        var analysis = new AnalysisResult { Intent = IntentType.Chat };

        var result = await _sut.IsAgentCreationRequestAsync("me ajude com código", analysis);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task HandleAgentCreationAsync_WithValidLLMResponse_ReturnsSuccess()
    {
        var input = "crie um agente de finanças";
        var context = new UserContext { UserId = "user1", Role = "dev", Language = "pt-br" };

        var llmJson = @"{
            ""name"": ""FinanceAgent"",
            ""description"": ""Agente de finanças"",
            ""domain"": ""finance"",
            ""tier"": ""Specialist"",
            ""allowedTools"": [],
            ""instructions"": ""You are a finance expert agent""
        }";

        _chatClient.GetResponseAsync(Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<ChatOptions?>(), Arg.Any<CancellationToken>())
            .Returns(new AIChatResponse(new ChatMessage(ChatRole.Assistant, llmJson)));

        var agent = Substitute.For<IAgent>();
        agent.Name.Returns("FinanceAgent");
        _agentFactory.CreateCustomAgentAsync(Arg.Any<AgentSpecification>())
            .Returns(agent);

        var result = await _sut.HandleAgentCreationAsync(input, context);

        result.Success.Should().BeTrue();
        result.Content.Should().Contain("FinanceAgent");
    }

    [Fact]
    public async Task HandleAgentCreationAsync_LLMFails_UsesFallbackSpec()
    {
        var input = "crie um agente de finanças";
        var context = new UserContext { UserId = "user1", Role = "dev", Language = "pt-br" };

        _chatClient.GetResponseAsync(Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<ChatOptions?>(), Arg.Any<CancellationToken>())
            .Returns(new AIChatResponse(new ChatMessage(ChatRole.Assistant, string.Empty)));

        var agent = Substitute.For<IAgent>();
        agent.Name.Returns("FinanceAgent");
        _agentFactory.CreateCustomAgentAsync(Arg.Any<AgentSpecification>())
            .Returns(agent);

        var result = await _sut.HandleAgentCreationAsync(input, context);

        result.Success.Should().BeTrue();
        await _agentFactory.Received(1).CreateCustomAgentAsync(
            Arg.Is<AgentSpecification>(s => s.Domain == "finance"));
    }

    [Fact]
    public async Task GetDynamicAgentsAsync_ExcludesBuiltInAgents()
    {
        var allAgents = new List<AgentInfo>
        {
            new() { Name = "PersonalAgent" },
            new() { Name = "WorkAgent" },
            new() { Name = "CustomFinanceAgent" }
        };
        _agentFactory.GetAllAgentsAsync().Returns(allAgents);

        var result = (await _sut.GetDynamicAgentsAsync()).ToList();

        result.Should().HaveCount(1);
        result[0].Name.Should().Be("CustomFinanceAgent");
    }

    [Fact]
    public async Task RemoveAgentAsync_DelegatesToFactory()
    {
        _agentFactory.RemoveAgentAsync("TestAgent").Returns(true);

        var result = await _sut.RemoveAgentAsync("TestAgent");

        result.Should().BeTrue();
        await _agentFactory.Received(1).RemoveAgentAsync("TestAgent");
    }

    [Fact]
    public async Task GenerateSpecificationAsync_LLMFails_ReturnsFallbackSpec()
    {
        var context = new UserContext { UserId = "user1", Role = "dev", Language = "pt-br" };

        _chatClient.GetResponseAsync(Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<ChatOptions?>(), Arg.Any<CancellationToken>())
            .Returns(new AIChatResponse(new ChatMessage(ChatRole.Assistant, string.Empty)));

        var spec = await _sut.GenerateSpecificationAsync("crie um agente de marketing", context);

        spec.Should().NotBeNull();
        spec.Domain.Should().Be("marketing");
    }
}
