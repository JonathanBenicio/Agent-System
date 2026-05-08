using FluentAssertions;

using Microsoft.Extensions.Logging;
using NSubstitute;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Core.Services;

namespace AgenticSystem.Tests;

public class HierarchicalAgentFactoryTests
{
    private readonly ISkillManager _skillManager;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<HierarchicalAgentFactory> _logger;
    private readonly HierarchicalAgentFactory _sut;

    public HierarchicalAgentFactoryTests()
    {
        _skillManager = Substitute.For<ISkillManager>();
        _loggerFactory = Substitute.For<ILoggerFactory>();
        _loggerFactory.CreateLogger(Arg.Any<string>()).Returns(Substitute.For<ILogger>());
        _logger = Substitute.For<ILogger<HierarchicalAgentFactory>>();
        _sut = new HierarchicalAgentFactory(_skillManager, _loggerFactory, _logger);
    }

    [Fact]
    public async Task ResolveAgentAsync_WithGeneralDomain_ReturnsAgent()
    {
        var analysis = new AnalysisResult
        {
            PrimaryDomain = "general",
            RecommendedTier = AgentTier.Support,
            Complexity = ComplexityLevel.Simple
        };

        var agent = await _sut.ResolveAgentAsync(analysis);

        agent.Should().NotBeNull();
        agent.Name.Should().Be("GeneralAgent");
    }

    [Fact]
    public async Task ResolveAgentAsync_WithPersonalDomain_ReturnsPersonalAgent()
    {
        var analysis = new AnalysisResult
        {
            PrimaryDomain = "personal",
            RecommendedTier = AgentTier.Master
        };

        var agent = await _sut.ResolveAgentAsync(analysis);

        agent.Should().NotBeNull();
        agent.Name.Should().Be("PersonalAgent");
    }

    [Theory]
    [InlineData(ComplexityLevel.Simple, AgentTier.Support)]
    [InlineData(ComplexityLevel.Moderate, AgentTier.Specialist)]
    [InlineData(ComplexityLevel.Complex, AgentTier.Master)]
    [InlineData(ComplexityLevel.RequiresPlanning, AgentTier.Chief)]
    public void DetermineTier_MapsCorrectly(ComplexityLevel complexity, AgentTier expectedTier)
    {
        var tier = _sut.DetermineTier(complexity);
        tier.Should().Be(expectedTier);
    }

    [Fact]
    public async Task GetAgentsByTierAsync_ReturnsCorrectAgents()
    {
        // Default agents include PersonalAgent(Master), WorkAgent(Master), 
        // LearningAgent(Master), GeneralAgent(Support)
        var supportAgents = await _sut.GetAgentsByTierAsync(AgentTier.Support);
        supportAgents.Should().Contain(a => a.Name == "GeneralAgent");
    }

    [Fact]
    public async Task CreateCustomAgentAsync_CreatesAndReturnsAgent()
    {
        var spec = new AgentSpecification
        {
            Name = "CustomTest",
            Description = "A test agent",
            Tier = AgentTier.Specialist,
            Domain = "testing"
        };

        var agent = await _sut.CreateCustomAgentAsync(spec);

        agent.Name.Should().Be("CustomTest");
        agent.Tier.Should().Be(AgentTier.Specialist);
    }
}
