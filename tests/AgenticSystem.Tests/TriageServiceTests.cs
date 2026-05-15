using System.Text.Json;
using AgenticSystem.Core.Models.Triage;
using AgenticSystem.Core.Services.Triage;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using MChatMessage = Microsoft.Extensions.AI.ChatMessage;
using MChatResponse = Microsoft.Extensions.AI.ChatResponse;

namespace AgenticSystem.Tests;

public class TriageServiceTests
{
    private readonly IChatClient _chatClient;
    private readonly ILogger<TriageService> _logger;
    private readonly TriageService _sut;

    public TriageServiceTests()
    {
        _chatClient = Substitute.For<IChatClient>();
        _logger = Substitute.For<ILogger<TriageService>>();
        _sut = new TriageService(_chatClient, _logger);
    }

    [Fact]
    public async Task AnalyzeComplexityAsync_WhenResponseIsChatty_ExtractsJsonSuccessfully()
    {
        // Arrange
        var chattyResponse = @"Com certeza! Aqui está a análise da sua requisição:

```json
{
  ""Intent"": ""DirectAnswer"",
  ""Complexity"": ""Low"",
  ""RequiresRAG"": false,
  ""RequiresTools"": false,
  ""RecommendedAgentTier"": ""Support"",
  ""EstimatedAgent"": ""GeneralAgent""
}
```

Espero que isso ajude!";

        _chatClient.GetResponseAsync(Arg.Any<IEnumerable<MChatMessage>>(), Arg.Any<ChatOptions>(), Arg.Any<CancellationToken>())
            .Returns(new MChatResponse(new MChatMessage(ChatRole.Assistant, chattyResponse)));

        // Act
        var result = await _sut.AnalyzeComplexityAsync("Qual a data de hoje?");

        // Assert
        result.Should().NotBeNull();
        result.Intent.Should().Be(IntentType.DirectAnswer);
        result.Complexity.Should().Be(ComplexityLevel.Low);
        result.EstimatedAgent.Should().Be("GeneralAgent");
    }

    [Fact]
    public async Task AnalyzeComplexityAsync_WhenResponseHasLeadingTextWithoutBlocks_ExtractsJsonSuccessfully()
    {
        // Arrange
        var leadingTextResponse = @"Análise:
{
  ""Intent"": ""ComplexReasoning"",
  ""Complexity"": ""High"",
  ""RequiresRAG"": true,
  ""RequiresTools"": true,
  ""RecommendedAgentTier"": ""Chief"",
  ""EstimatedAgent"": ""Orchestrator""
}";

        _chatClient.GetResponseAsync(Arg.Any<IEnumerable<MChatMessage>>(), Arg.Any<ChatOptions>(), Arg.Any<CancellationToken>())
            .Returns(new MChatResponse(new MChatMessage(ChatRole.Assistant, leadingTextResponse)));

        // Act
        var result = await _sut.AnalyzeComplexityAsync("Crie um plano de migração.");

        // Assert
        result.Should().NotBeNull();
        result.Intent.Should().Be(IntentType.ComplexReasoning);
        result.Complexity.Should().Be(ComplexityLevel.High);
    }

    [Fact]
    public async Task AnalyzeComplexityAsync_WhenResponseIsPureJson_WorksNormally()
    {
        // Arrange
        var pureJsonResponse = @"{
  ""Intent"": ""SmallTalk"",
  ""Complexity"": ""Low"",
  ""RequiresRAG"": false,
  ""RequiresTools"": false,
  ""RecommendedAgentTier"": ""Support"",
  ""EstimatedAgent"": ""GeneralAgent""
}";

        _chatClient.GetResponseAsync(Arg.Any<IEnumerable<MChatMessage>>(), Arg.Any<ChatOptions>(), Arg.Any<CancellationToken>())
            .Returns(new MChatResponse(new MChatMessage(ChatRole.Assistant, pureJsonResponse)));

        // Act
        var result = await _sut.AnalyzeComplexityAsync("Olá!");

        // Assert
        result.Should().NotBeNull();
        result.Intent.Should().Be(IntentType.SmallTalk);
    }
}
