using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Core.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace AgenticSystem.Tests;

public sealed class SelfImprovementServiceTests
{
    [Fact]
    public async Task DisabledFeatureDoesNotCreateOrApplyProposals()
    {
        var accessor = new TenantContextAccessor();
        using var tenantScope = accessor.BeginScope(new TenantContext { TenantId = "tenant-a" });
        var proposalStore = new InMemorySelfImprovementProposalStore(accessor);
        var audit = Substitute.For<IAuditLog>();
        var service = new SelfImprovementService(
            Substitute.For<IOperationalStore>(),
            proposalStore,
            Substitute.For<IAgentFactory>(),
            Substitute.For<IAgentVersionStore>(),
            Substitute.For<IPromptManager>(),
            audit,
            accessor,
            Options.Create(new SelfImprovementSettings { Enabled = false }));

        var result = await service.AnalyzeAndImproveAsync("Analyst");

        result.Status.Should().Be("Disabled");
        (await service.GetProposalsAsync()).Should().BeEmpty();
        (await service.ApproveProposalAsync(result.Id, "owner-1")).Should().BeFalse();
        await audit.DidNotReceive().RecordAsync(Arg.Any<AuditEntry>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProposalRequiresApprovalAndCanBeRolledBackWithVersionHistory()
    {
        var accessor = new TenantContextAccessor();
        using var tenantScope = accessor.BeginScope(new TenantContext { TenantId = "tenant-a" });
        var templates = new InMemoryPromptTemplateStore(accessor);
        await templates.SaveAsync(new PromptTemplate
        {
            Id = "baseline-v1",
            Name = "baseline",
            AgentName = "Analyst",
            TenantId = "tenant-a",
            TemplateBody = "You are a helpful analyst.",
            Version = 1
        });
        var promptManager = new PromptManager(
            templates,
            _ => null,
            Substitute.For<ILogger<PromptManager>>());
        using (accessor.BeginScope(new TenantContext { TenantId = "tenant-b" }))
            (await promptManager.GetActiveTemplateAsync("Analyst")).Should().BeNull();

        var reflections = new List<Reflection>
        {
            new()
            {
                AgentName = "Analyst",
                Severity = ReflectionSeverity.Critical,
                LessonsLearned = ["verify source data before answering"]
            }
        };
        var operationalStore = Substitute.For<IOperationalStore>();
        operationalStore.GetRecentLearningsAsync(50, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<Reflection>>(reflections));
        var proposals = new InMemorySelfImprovementProposalStore(accessor);
        var audit = Substitute.For<IAuditLog>();
        var agentFactory = Substitute.For<IAgentFactory>();
        agentFactory.GetAllAgentsAsync().Returns([new AgentInfo
        {
            Name = "Analyst",
            Description = "Analyst agent",
            Tier = AgentTier.Specialist,
            Domain = "general",
            AvailableTools = ["search"]
        }]);
        agentFactory.CreateCustomAgentAsync(Arg.Any<AgentSpecification>())
            .Returns(Substitute.For<IAgent>());
        var versionStore = Substitute.For<IAgentVersionStore>();
        versionStore.GetNextVersionNumberAsync("Analyst", Arg.Any<CancellationToken>()).Returns(1, 2);
        var service = new SelfImprovementService(
            operationalStore,
            proposals,
            agentFactory,
            versionStore,
            promptManager,
            audit,
            accessor,
            Options.Create(new SelfImprovementSettings { Enabled = true }));

        var proposal = await service.AnalyzeAndImproveAsync("Analyst");

        proposal.Status.Should().Be("Proposed");
        (await promptManager.GetActiveTemplateAsync("Analyst"))!.TemplateBody.Should().Be("You are a helpful analyst.");
        (await service.GetProposalsAsync()).Should().ContainSingle();

        (await service.ApproveProposalAsync(proposal.Id, "owner-1")).Should().BeTrue();
        var approvedTemplate = await promptManager.GetActiveTemplateAsync("Analyst");
        approvedTemplate!.Version.Should().Be(2);
        approvedTemplate.TemplateBody.Should().Contain("verify source data before answering");
        (await service.GetProposalsAsync()).Single().AppliedAgentVersionId.Should().NotBeNullOrWhiteSpace();

        (await service.RollbackProposalAsync(proposal.Id, "owner-1")).Should().BeTrue();
        var rolledBackTemplate = await promptManager.GetActiveTemplateAsync("Analyst");
        rolledBackTemplate!.Version.Should().Be(3);
        rolledBackTemplate.TemplateBody.Should().Be("You are a helpful analyst.");
        (await service.GetProposalsAsync()).Single().Status.Should().Be("RolledBack");
        await agentFactory.Received(1).CreateCustomAgentAsync(Arg.Is<AgentSpecification>(specification =>
            specification.Instructions.Contains("verify source data before answering")));
        await agentFactory.Received(1).CreateCustomAgentAsync(Arg.Is<AgentSpecification>(specification =>
            specification.Instructions == "You are a helpful analyst."));
        await versionStore.Received(2).SaveAsync(Arg.Is<AgentVersion>(version =>
            version.AgentName == "Analyst" && version.Environment == AgentVersionEnvironment.Production &&
            version.Status == AgentVersionStatus.Active), Arg.Any<CancellationToken>());
        await audit.Received(5).RecordAsync(Arg.Any<AuditEntry>(), Arg.Any<CancellationToken>());
    }
}
