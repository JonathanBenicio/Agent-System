using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.AgentFramework;
using FluentAssertions;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Tests;

public class OrchestratorInstructionServiceTests
{
    [Fact]
    public void GetInstructions_RefreshesCachedPromptWhenDynamicAgentDescriptorChanges()
    {
        var service = new OrchestratorInstructionService(Substitute.For<ILogger<OrchestratorInstructionService>>());
        var original = new AgentInfo
        {
            Name = "ResearchAgent",
            Description = "Searches public research papers.",
            Domain = "research",
            Tier = AgentTier.Specialist,
            IsActive = true,
            AvailableTools = ["paper.search"]
        };
        var updated = new AgentInfo
        {
            Name = "ResearchAgent",
            Description = "Searches tenant-specific internal reports.",
            Domain = "tenant-analytics",
            Tier = AgentTier.Master,
            IsActive = true,
            AvailableTools = ["report.search"]
        };

        var originalPrompt = service.GetInstructions([original], []);
        var updatedPrompt = service.GetInstructions([updated], []);

        originalPrompt.Should().Contain("Searches public research papers.");
        updatedPrompt.Should().Contain("Searches tenant-specific internal reports.");
        updatedPrompt.Should().Contain("tenant-analytics");
        updatedPrompt.Should().Contain("report.search");
        updatedPrompt.Should().NotContain("Searches public research papers.");
    }
}
