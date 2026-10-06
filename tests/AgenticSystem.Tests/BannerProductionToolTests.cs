using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Core.Services;
using AgenticSystem.Infrastructure.Tools;
using FluentAssertions;
using NSubstitute;

namespace AgenticSystem.Tests;

public sealed class BannerProductionToolTests
{
    [Fact]
    public async Task ExecuteAsync_StartsTenantOwnedDefinitionThroughCanonicalWorkflowEngine()
    {
        var tenantAccessor = new TenantContextAccessor();
        var store = Substitute.For<IWorkflowStore>();
        var engine = Substitute.For<IWorkflowEngine>();
        var definition = new WorkflowDefinition
        {
            Id = "banner-production-tenant-a",
            Name = "Banner Production Workflow",
            Steps = [new WorkflowStep { Id = "render", Name = "Render", StepType = WorkflowStepType.Agent, AgentName = "BannerAgent" }]
        };
        store.GetDefinitionAsync("tenant-a", "banner-production", Arg.Any<CancellationToken>()).Returns((WorkflowDefinition?)null);
        store.ListDefinitionsAsync("tenant-a", 100, Arg.Any<CancellationToken>()).Returns([definition]);
        engine.StartAsync(
                "tenant-a", definition, Arg.Any<Dictionary<string, object>>(), "user-a", Arg.Any<CancellationToken>())
            .Returns(new WorkflowExecution
            {
                Id = "execution-123",
                TenantId = "tenant-a",
                WorkflowId = "banner-production",
                Status = WorkflowExecutionStatus.Pending
            });
        var tool = new BannerProductionTool(tenantAccessor, store, engine);
        var input = new ToolInput
        {
            Action = "generate",
            UserId = "user-a",
            Parameters = new Dictionary<string, object>
            {
                ["imagePath"] = "listing.jpg",
                ["price"] = 425000m,
                ["bedrooms"] = 3
            }
        };

        using var tenantScope = tenantAccessor.BeginScope(new TenantContext { TenantId = "tenant-a" });
        var result = await tool.ExecuteAsync(input);

        result.Success.Should().BeTrue();
        result.Metadata.Should().ContainKey("executionId").WhoseValue.Should().Be("execution-123");
        result.Metadata.Should().ContainKey("statusUrl").WhoseValue.Should().Be("/api/workflow/executions/execution-123");
        await store.Received(1).GetDefinitionAsync("tenant-a", "banner-production", Arg.Any<CancellationToken>());
        await store.DidNotReceive().GetDefinitionAsync("admin", "banner-production", Arg.Any<CancellationToken>());
        await engine.Received(1).StartAsync(
            "tenant-a",
            definition,
            Arg.Is<Dictionary<string, object>>(variables =>
                variables["imagePath"].Equals("listing.jpg")
                && variables["price"].Equals(425000m)
                && variables["bedrooms"].Equals(3)),
            "user-a",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotFallBackToAnotherTenantsDefinition()
    {
        var tenantAccessor = new TenantContextAccessor();
        var store = Substitute.For<IWorkflowStore>();
        var engine = Substitute.For<IWorkflowEngine>();
        store.GetDefinitionAsync("tenant-a", "banner-production", Arg.Any<CancellationToken>()).Returns((WorkflowDefinition?)null);
        store.ListDefinitionsAsync("tenant-a", 100, Arg.Any<CancellationToken>()).Returns(Array.Empty<WorkflowDefinition>());
        var tool = new BannerProductionTool(tenantAccessor, store, engine);
        using var tenantScope = tenantAccessor.BeginScope(new TenantContext { TenantId = "tenant-a" });

        var result = await tool.ExecuteAsync(new ToolInput
        {
            Action = "generate",
            UserId = "user-a",
            Parameters = new Dictionary<string, object>
            {
                ["imagePath"] = "listing.jpg",
                ["price"] = 425000m,
                ["bedrooms"] = 3
            }
        });

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not configured for the active tenant");
        await store.DidNotReceive().GetDefinitionAsync("admin", "banner-production", Arg.Any<CancellationToken>());
        await engine.DidNotReceive().StartAsync(
            Arg.Any<string>(), Arg.Any<WorkflowDefinition>(), Arg.Any<Dictionary<string, object>>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
