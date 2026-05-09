using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Core.Services;
using AgenticSystem.Core.LLM.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace AgenticSystem.Tests;

public class Phase3IntelligenceTests
{
    [Fact]
    public async Task AdaptiveModelRouter_ShouldSelectBestModelBasedOnPriority()
    {
        // Arrange
        var perfStore = new InMemoryModelPerformanceStore();
        var llmAdmin = Substitute.For<ILLMAdministrationService>();
        var logger = Substitute.For<ILogger<AdaptiveModelRouter>>();

        llmAdmin.GetConfigurationAsync(Arg.Any<CancellationToken>())
            .Returns(new LLMConfigurationInfo
            {
                Providers = new List<LLMProviderInfo>
                {
                    new() { Name = "OpenAI", DefaultModel = "gpt-4o", IsEnabled = true, Priority = 1, IsAvailable = true },
                    new() { Name = "Gemini", DefaultModel = "gemini-1.5-flash", IsEnabled = true, Priority = 2, IsAvailable = true }
                }
            });

        // Record some bad performance for OpenAI to see if it routes away
        await perfStore.RecordPerformanceAsync("gpt-4o", new ModelPerformanceRecord
        {
            LatencyMs = 3000,
            Success = false,
            RecordedAt = DateTime.UtcNow
        });

        var router = new AdaptiveModelRouter(perfStore, llmAdmin, logger);

        // Act
        var decision = await router.RouteToModelAsync(new ModelRoutingRequest
        {
            TaskDescription = "Summarize text",
            Priority = ModelRoutingPriority.Speed
        });

        // Assert
        decision.ModelId.Should().Be("gemini-1.5-flash"); // Flash is usually faster, and we marked 4o with failure
        decision.ConfidenceScore.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task DefaultCitationEngine_ShouldInjectMarkersIntoResponse()
    {
        // Arrange
        var logger = Substitute.For<ILogger<DefaultCitationEngine>>();
        var engine = new DefaultCitationEngine(logger);
        
        var response = "Clean Architecture is a software design philosophy. Separation of concerns is a key goal.";
        var chunks = new List<RankedChunk>
        {
            new() { Id = "c1", Content = "Clean Architecture is a software design philosophy developed by Robert C. Martin.", Source = "Uncle Bob", ReRankedScore = 0.9 },
            new() { Id = "c2", Content = "Separation of concerns helps manage complexity in software.", Source = "Wiki", ReRankedScore = 0.8 }
        };

        // Act
        var result = await engine.GenerateWithCitationsAsync(response, chunks);

        // Assert
        result.CitedText.Should().Contain("[1]");
        result.CitedText.Should().Contain("[2]");
        result.Citations.Should().HaveCount(2);
        result.Citations[0].SourceDocumentName.Should().Be("Uncle Bob");
    }

    [Fact]
    public async Task AgentSimulationService_ShouldRunInputsAndReturnResults()
    {
        // Arrange
        var agentExecutor = Substitute.For<IDirectAgentRequestExecutor>();
        var logger = Substitute.For<ILogger<AgentSimulationService>>();
        
        agentExecutor.ExecuteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<UserContext>(), "MetaAgent")
            .Returns(new AgentResponse { Success = true, Content = "Simulated response" });

        var simulator = new AgentSimulationService(agentExecutor, logger);
        var config = new SimulationConfig
        {
            MockResponses = new Dictionary<string, string> { ["Hello"] = "Hi" }
        };

        // Act
        var result = await simulator.RunSimulationAsync(config);

        // Assert
        result.WouldSucceed.Should().BeTrue();
        result.Actions.Should().HaveCount(1);
        result.Actions[0].MockResult.Should().Be("Simulated response");
    }
}
