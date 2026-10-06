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
    private readonly IDynamicAgentRepository _dynamicAgentRepository;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<HierarchicalAgentFactory> _logger;
    private readonly HierarchicalAgentFactory _sut;

    public HierarchicalAgentFactoryTests()
    {
        _skillManager = Substitute.For<ISkillManager>();
        _dynamicAgentRepository = Substitute.For<IDynamicAgentRepository>();
        _loggerFactory = Substitute.For<ILoggerFactory>();
        _loggerFactory.CreateLogger(Arg.Any<string>()).Returns(Substitute.For<ILogger>());
        _logger = Substitute.For<ILogger<HierarchicalAgentFactory>>();
        
        _dynamicAgentRepository.GetAllAsync().Returns(Task.FromResult((IEnumerable<AgentSpecification>)new List<AgentSpecification>()));
        
        _sut = new HierarchicalAgentFactory(_skillManager, _dynamicAgentRepository, _loggerFactory, _logger);
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
    [Fact]
    public async Task ResolveAgentAsync_WithDotNetDomain_ReturnsDotNetExpertAgent()
    {
        var analysis = new AnalysisResult
        {
            PrimaryDomain = "dotnet",
            RecommendedTier = AgentTier.Specialist
        };

        var agent = await _sut.ResolveAgentAsync(analysis);

        agent.Should().NotBeNull();
        agent.Name.Should().Be("DotNetExpertAgent");
    }

    [Fact]
    public async Task ResolveAgentAsync_ScopesSameDynamicAgentNameAndRefreshesByTenant()
    {
        var tenantAccessor = new TenantContextAccessor();
        var tenantASpec = new AgentSpecification
        {
            Name = "SharedSpecialist",
            Description = "Tenant A specialist",
            Domain = "research",
            Instructions = "Tenant A instructions"
        };
        var tenantBSpec = new AgentSpecification
        {
            Name = "SharedSpecialist",
            Description = "Tenant B specialist",
            Domain = "research",
            Instructions = "Tenant B instructions"
        };
        _dynamicAgentRepository.GetByNameAsync("SharedSpecialist", Arg.Any<CancellationToken>())
            .Returns(_ => tenantAccessor.CurrentTenantId == "tenant-a" ? tenantASpec : tenantBSpec);
        var factory = new HierarchicalAgentFactory(
            _skillManager,
            _dynamicAgentRepository,
            _loggerFactory,
            _logger,
            tenantContextAccessor: tenantAccessor);

        IAgent tenantAAgent;
        using (tenantAccessor.BeginScope(new TenantContext { TenantId = "tenant-a" }))
            tenantAAgent = await factory.ResolveAgentAsync(new AgentInfo { Name = "SharedSpecialist" });

        IAgent tenantBAgent;
        using (tenantAccessor.BeginScope(new TenantContext { TenantId = "tenant-b" }))
            tenantBAgent = await factory.ResolveAgentAsync(new AgentInfo { Name = "SharedSpecialist" });

        tenantAAgent.Should().NotBeSameAs(tenantBAgent);
        tenantAAgent.Instructions.Should().Be("Tenant A instructions");
        tenantBAgent.Instructions.Should().Be("Tenant B instructions");

        tenantASpec.Instructions = "Tenant A updated instructions";
        using (tenantAccessor.BeginScope(new TenantContext { TenantId = "tenant-a" }))
        {
            var refreshed = await factory.ResolveAgentAsync(new AgentInfo { Name = "SharedSpecialist" });
            refreshed.Instructions.Should().Be("Tenant A updated instructions");
        }
    }
}
