using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Core.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace AgenticSystem.Tests;

public class WorkflowEngineTests
{
    private readonly IWorkflowStore _store;
    private readonly IDirectAgentRequestExecutor _agentExecutor;
    private readonly IToolManager _toolManager;
    private readonly IPermissionService _permissionService;
    private readonly DefaultWorkflowEngine _engine;
    private const string TenantId = "default";

    public WorkflowEngineTests()
    {
        _store = new InMemoryWorkflowStore();
        _agentExecutor = Substitute.For<IDirectAgentRequestExecutor>();
        _toolManager = Substitute.For<IToolManager>();
        _permissionService = Substitute.For<IPermissionService>();
        _permissionService.HasPermissionAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Permission>(), Arg.Any<CancellationToken>())
            .Returns(true);
        _engine = new DefaultWorkflowEngine(
            _store,
            _agentExecutor,
            _toolManager,
            Substitute.For<ILogger<DefaultWorkflowEngine>>(),
            permissionService: _permissionService);
    }

    [Fact]
    public async Task AgentStep_ReceivesSnapshotPromptDependencyOutputAndRestrictedRuntimeOptions()
    {
        var seen = new List<(string Input, UserContext Context, string Agent)>();
        _agentExecutor.ExecuteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<UserContext>(),
                Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                seen.Add((call.ArgAt<string>(1), call.ArgAt<UserContext>(2), call.ArgAt<string>(3)));
                return new AgentResponse { Success = true, Content = seen.Count == 1 ? "Foto analisada" : "Banner pronto" };
            });
        var definition = new WorkflowDefinition
        {
            Id = "wf-banner-options", Name = "Banner", PromptTemplate = "Preço {{price}}; imagem {{imagePath}}",
            Steps =
            [
                new WorkflowStep { Id = "vision", Name = "Vision", StepType = WorkflowStepType.Agent,
                    AgentName = "VisionAnalyst", ModelOverride = "vision-model" },
                new WorkflowStep { Id = "editor", Name = "Editor", StepType = WorkflowStepType.Agent,
                    AgentName = "EditorChefe", DependsOn = ["vision"], ModelOverride = "editor-model",
                    AllowedToolsOverride = ["CleanImageAsync", "RenderBannerAsync"] }
            ]
        };
        var started = await _engine.StartAsync(TenantId, definition,
            new Dictionary<string, object> { ["price"] = 650000, ["imagePath"] = "photo.png" }, "owner");
        await ProcessClaimedExecutionAsync(started.Id);

        seen.Should().HaveCount(2);
        seen[0].Input.Should().Contain("Preço 650000; imagem photo.png");
        seen[0].Context.WorkflowOptions!.Model.Should().Be("vision-model");
        seen[1].Input.Should().Contain("Foto analisada");
        seen[1].Context.WorkflowOptions!.Model.Should().Be("editor-model");
        seen[1].Context.WorkflowOptions!.AllowedTools.Should().BeEquivalentTo(["CleanImageAsync", "RenderBannerAsync"]);
        seen[1].Context.WorkflowOptions!.SessionId.Should().Be($"workflow:{started.Id}:editor");
    }

    [Fact]
    public async Task WorkflowLease_ExcludesConcurrentWorkerAndAllowsRecoveryAfterExpiry()
    {
        var execution = await _engine.StartAsync(TenantId, new WorkflowDefinition
        {
            Id = "wf-lease",
            Name = "Lease test",
            Steps = [new WorkflowStep { Id = "step", Name = "Step", StepType = WorkflowStepType.Wait, Timeout = TimeSpan.FromMinutes(1) }]
        });

        var firstClaim = await _store.ClaimNextExecutionAsync("worker-a", TimeSpan.FromMilliseconds(20));
        firstClaim.Should().NotBeNull();
        firstClaim!.ExecutionId.Should().Be(execution.Id);
        (await _store.ClaimNextExecutionAsync("worker-b", TimeSpan.FromMinutes(1))).Should().BeNull();

        await Task.Delay(40);

        var recoveredClaim = await _store.ClaimNextExecutionAsync("worker-b", TimeSpan.FromMinutes(1));
        recoveredClaim.Should().NotBeNull();
        recoveredClaim!.WorkerId.Should().Be("worker-b");
        (await _store.RenewExecutionLeaseAsync(firstClaim, TimeSpan.FromMinutes(1))).Should().BeFalse();
        await _store.ReleaseExecutionLeaseAsync(recoveredClaim);
    }

    [Fact]
    public async Task InMemoryWorkflowStore_ScopesDefinitionsAndRejectsCrossTenantIdReuse()
    {
        var definition = new WorkflowDefinition { Id = "shared-definition-id", Name = "Tenant A definition" };
        await _store.SaveDefinitionAsync("tenant-a", definition);

        (await _store.GetDefinitionAsync("tenant-b", definition.Id)).Should().BeNull();
        (await _store.ListDefinitionsAsync("tenant-b")).Should().BeEmpty();
        var crossTenantSave = () => _store.SaveDefinitionAsync(
            "tenant-b",
            new WorkflowDefinition { Id = definition.Id, Name = "Tenant B overwrite" });
        await crossTenantSave.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Workflow definition tenant ownership cannot be changed.");

        (await _store.GetDefinitionAsync("tenant-a", definition.Id))!.Name.Should().Be("Tenant A definition");
    }

    [Fact]
    public async Task ToolStep_RequiresInitiatorPermissionAndPassesStableIdempotencyKey()
    {
        _toolManager.ExecuteToolAsync("external-tool", Arg.Any<ToolInput>(), Arg.Any<CancellationToken>())
            .Returns(ToolResult.Ok("done"));
        var definition = new WorkflowDefinition
        {
            Id = "wf-tool-auth",
            Name = "Tool authorization",
            Steps = [new WorkflowStep { Id = "publish", Name = "Publish", StepType = WorkflowStepType.Action, ToolName = "external-tool" }]
        };

        var started = await _engine.StartAsync(TenantId, definition, initiatedBy: "user-allowed");
        await ProcessClaimedExecutionAsync(started.Id);

        await _permissionService.Received(1).HasPermissionAsync(
            "user-allowed", "tools/external-tool", Permission.Execute, Arg.Any<CancellationToken>());
        await _toolManager.Received(1).ExecuteToolAsync(
            "external-tool",
            Arg.Is<ToolInput>(input => input.UserId == "user-allowed" && input.IdempotencyKey == $"{started.Id}:publish"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ToolStep_WithoutExecutePermissionFailsWithoutCallingTool()
    {
        _permissionService.HasPermissionAsync(
                "user-denied", "tools/external-tool", Permission.Execute, Arg.Any<CancellationToken>())
            .Returns(false);
        var started = await _engine.StartAsync(TenantId, new WorkflowDefinition
        {
            Id = "wf-tool-denied",
            Name = "Denied tool",
            Steps = [new WorkflowStep { Id = "publish", Name = "Publish", StepType = WorkflowStepType.Action, ToolName = "external-tool" }]
        }, initiatedBy: "user-denied");

        await ProcessClaimedExecutionAsync(started.Id);

        (await _engine.GetExecutionAsync(TenantId, started.Id))!.Status.Should().Be(WorkflowExecutionStatus.Failed);
        await _toolManager.DidNotReceive().ExecuteToolAsync(
            Arg.Any<string>(), Arg.Any<ToolInput>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ToolStep_RetriesConfiguredAttemptsWithTheSameIdempotencyKey()
    {
        var keys = new List<string?>();
        _toolManager.ExecuteToolAsync(
                "external-tool",
                Arg.Do<ToolInput>(input => keys.Add(input.IdempotencyKey)),
                Arg.Any<CancellationToken>())
            .Returns(
                _ => ToolResult.Fail("transient failure"),
                _ => ToolResult.Ok("completed"));
        var started = await _engine.StartAsync(TenantId, new WorkflowDefinition
        {
            Id = "wf-tool-retry",
            Name = "Retry idempotent action",
            Steps =
            [
                new WorkflowStep
                {
                    Id = "publish",
                    Name = "Publish",
                    StepType = WorkflowStepType.Action,
                    ToolName = "external-tool",
                    MaxRetries = 1
                }
            ]
        }, initiatedBy: "user-allowed");

        await ProcessClaimedExecutionAsync(started.Id);

        (await _engine.GetExecutionAsync(TenantId, started.Id))!.Status.Should().Be(WorkflowExecutionStatus.Completed);
        await _toolManager.Received(2).ExecuteToolAsync(
            "external-tool", Arg.Any<ToolInput>(), Arg.Any<CancellationToken>());
        keys.Should().Equal($"{started.Id}:publish", $"{started.Id}:publish");
    }

    [Fact]
    public async Task StartAsync_ShouldInitializeAndRunSteps()
    {
        // Arrange
        var definition = new WorkflowDefinition
        {
            Id = "wf-1",
            Name = "Test Workflow",
            Steps = new List<WorkflowStep>
            {
                new() { Id = "step-1", Name = "Step 1", AgentName = "AgentA", ActionDescription = "Do A" },
                new() { Id = "step-2", Name = "Step 2", AgentName = "AgentB", ActionDescription = "Do B", DependsOn = new List<string> { "step-1" } }
            }
        };

        _agentExecutor.ExecuteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<UserContext>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new AgentResponse { Success = true, Content = "Done" });

        await _store.SaveDefinitionAsync(TenantId, definition);

        // Act
        var execution = await _engine.StartAsync(TenantId, definition, initiatedBy: "user-1");

        // Assert
        execution.Status.Should().Be(WorkflowExecutionStatus.Pending);
        execution.WorkflowId.Should().Be("wf-1");

        // Give it some time to process background tasks
        await ProcessClaimedExecutionAsync(execution.Id);

        var finalState = await _engine.GetExecutionAsync(TenantId, execution.Id);
        finalState!.Status.Should().Be(WorkflowExecutionStatus.Completed);
        finalState.StepExecutions.Should().HaveCount(2);
        finalState.StepExecutions.All(s => s.Status == WorkflowExecutionStatus.Completed).Should().BeTrue();
    }

    [Fact]
    public async Task StartAsync_WithAgentStepType_ExecutesTheConfiguredAgent()
    {
        var definition = new WorkflowDefinition
        {
            Id = "wf-agent-step",
            Name = "Agent Step Workflow",
            Steps =
            [
                new WorkflowStep
                {
                    Id = "agent-step",
                    Name = "Research",
                    StepType = WorkflowStepType.Agent,
                    AgentName = "ResearchAgent",
                    ActionDescription = "Research the topic"
                }
            ]
        };
        _agentExecutor.ExecuteAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<UserContext>(),
                "ResearchAgent",
                Arg.Any<CancellationToken>())
            .Returns(new AgentResponse { Success = true, Content = "Research complete" });

        var started = await _engine.StartAsync(TenantId, definition, initiatedBy: "user-1");
        await ProcessClaimedExecutionAsync(started.Id);

        var finalState = await _engine.GetExecutionAsync(TenantId, started.Id);
        finalState!.Status.Should().Be(WorkflowExecutionStatus.Completed);
        finalState.StepExecutions.Should().ContainSingle()
            .Which.Output["content"].Should().Be("Research complete");
        await _agentExecutor.Received(1).ExecuteAsync(
            started.Id,
            "Research the topic",
            Arg.Is<UserContext>(context => context.TenantId == TenantId && context.UserId == "user-1"),
            "ResearchAgent",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ApprovalStep_PausesUntilApprovedThenResumesTheDependentAgent()
    {
        var definition = new WorkflowDefinition
        {
            Id = "wf-approval",
            Name = "Approval Workflow",
            Steps =
            [
                new WorkflowStep { Id = "approval", Name = "Review", StepType = WorkflowStepType.Approval },
                new WorkflowStep
                {
                    Id = "agent-step",
                    Name = "Publish",
                    StepType = WorkflowStepType.Agent,
                    AgentName = "Publisher",
                    DependsOn = ["approval"]
                }
            ]
        };
        _agentExecutor.ExecuteAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<UserContext>(), "Publisher", Arg.Any<CancellationToken>())
            .Returns(new AgentResponse { Success = true, Content = "Published" });
        await _store.SaveDefinitionAsync(TenantId, definition);

        var started = await _engine.StartAsync(TenantId, definition, initiatedBy: "user-1");
        await ProcessClaimedExecutionAsync(started.Id);

        var waiting = await _engine.GetExecutionAsync(TenantId, started.Id);
        waiting!.Status.Should().Be(WorkflowExecutionStatus.WaitingForApproval);
        waiting.StepExecutions.Should().ContainSingle(step => step.StepId == "approval"
            && step.Status == WorkflowExecutionStatus.WaitingForApproval);
        await _agentExecutor.DidNotReceive().ExecuteAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<UserContext>(), Arg.Any<string>(), Arg.Any<CancellationToken>());

        await _engine.ApproveAsync(TenantId, started.Id, "reviewer-1");
        await ProcessClaimedExecutionAsync(started.Id);

        var completed = await _engine.GetExecutionAsync(TenantId, started.Id);
        completed!.Status.Should().Be(WorkflowExecutionStatus.Completed);
        completed.StepExecutions.Single(step => step.StepId == "approval").Output["approvedBy"].Should().Be("reviewer-1");
        await _agentExecutor.Received(1).ExecuteAsync(
            started.Id,
            Arg.Any<string>(),
            Arg.Any<UserContext>(),
            "Publisher",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ApprovalStep_RejectionCancelsWithoutRunningDependentSteps()
    {
        var definition = new WorkflowDefinition
        {
            Id = "wf-rejected-approval",
            Name = "Rejected Approval Workflow",
            Steps =
            [
                new WorkflowStep { Id = "approval", Name = "Review", StepType = WorkflowStepType.Approval },
                new WorkflowStep
                {
                    Id = "agent-step",
                    Name = "Publish",
                    StepType = WorkflowStepType.Agent,
                    AgentName = "Publisher",
                    DependsOn = ["approval"]
                }
            ]
        };
        await _store.SaveDefinitionAsync(TenantId, definition);

        var started = await _engine.StartAsync(TenantId, definition, initiatedBy: "user-1");
        await ProcessClaimedExecutionAsync(started.Id);

        var rejected = await _engine.RejectAsync(TenantId, started.Id, "reviewer-1", "Needs revision");

        rejected.Status.Should().Be(WorkflowExecutionStatus.Cancelled);
        rejected.ErrorMessage.Should().Be("Needs revision");
        rejected.StepExecutions.Single(step => step.StepId == "approval").Status.Should().Be(WorkflowExecutionStatus.Failed);
        await _agentExecutor.DidNotReceive().ExecuteAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<UserContext>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ApprovalResume_UsesTheDefinitionVersionCapturedAtStart()
    {
        var original = new WorkflowDefinition
        {
            Id = "wf-snapshot",
            Name = "Snapshot Workflow",
            Version = 1,
            Steps =
            [
                new WorkflowStep { Id = "approval", Name = "Review", StepType = WorkflowStepType.Approval },
                new WorkflowStep { Id = "publish", Name = "Publish", StepType = WorkflowStepType.Agent, AgentName = "OriginalAgent", DependsOn = ["approval"] }
            ]
        };
        await _store.SaveDefinitionAsync(TenantId, original);
        _agentExecutor.ExecuteAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<UserContext>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new AgentResponse { Success = true, Content = "Done" });

        var started = await _engine.StartAsync(TenantId, original, initiatedBy: "user-1");
        await ProcessClaimedExecutionAsync(started.Id);

        var edited = new WorkflowDefinition
        {
            Id = original.Id,
            Name = original.Name,
            Version = 2,
            Steps =
            [
                new WorkflowStep { Id = "approval", Name = "Review", StepType = WorkflowStepType.Approval },
                new WorkflowStep { Id = "publish", Name = "Publish", StepType = WorkflowStepType.Agent, AgentName = "NewAgent", DependsOn = ["approval"] }
            ]
        };
        await _store.SaveDefinitionAsync(TenantId, edited);

        await _engine.ApproveAsync(TenantId, started.Id, "reviewer-1");
        await ProcessClaimedExecutionAsync(started.Id);

        var completed = await _engine.GetExecutionAsync(TenantId, started.Id);
        completed!.Status.Should().Be(WorkflowExecutionStatus.Completed);
        completed.WorkflowDefinitionVersion.Should().Be(1);
        await _agentExecutor.Received(1).ExecuteAsync(
            started.Id, Arg.Any<string>(), Arg.Any<UserContext>(), "OriginalAgent", Arg.Any<CancellationToken>());
        await _agentExecutor.DidNotReceive().ExecuteAsync(
            started.Id, Arg.Any<string>(), Arg.Any<UserContext>(), "NewAgent", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ApprovalResume_RejectsATamperedDefinitionSnapshot()
    {
        var definition = new WorkflowDefinition
        {
            Id = "wf-tampered-snapshot",
            Name = "Snapshot Integrity",
            Steps = [new WorkflowStep { Id = "approval", Name = "Review", StepType = WorkflowStepType.Approval }]
        };
        await _store.SaveDefinitionAsync(TenantId, definition);

        var started = await _engine.StartAsync(TenantId, definition);
        await ProcessClaimedExecutionAsync(started.Id);
        var waiting = await _engine.GetExecutionAsync(TenantId, started.Id);
        waiting!.WorkflowDefinitionSnapshotJson = waiting.WorkflowDefinitionSnapshotJson
            .Replace("Snapshot Integrity", "Tampered Integrity", StringComparison.Ordinal);
        await _store.SaveExecutionAsync(TenantId, waiting);

        var act = () => _engine.ApproveAsync(TenantId, started.Id, "reviewer-1");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*snapshot hash does not match*");
        await _agentExecutor.DidNotReceive().ExecuteAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<UserContext>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WaitStep_DoesNotCompleteBeforeItsConfiguredDelay()
    {
        var definition = new WorkflowDefinition
        {
            Id = "wf-wait",
            Name = "Wait Workflow",
            Steps = [new WorkflowStep { Id = "wait", Name = "Wait", StepType = WorkflowStepType.Wait, Timeout = TimeSpan.FromMilliseconds(300) }]
        };

        var started = await _engine.StartAsync(TenantId, definition);
        await ProcessClaimedExecutionAsync(started.Id);

        var waiting = await _engine.GetExecutionAsync(TenantId, started.Id);
        waiting!.Status.Should().Be(WorkflowExecutionStatus.Pending);
        waiting.StepExecutions.Should().ContainSingle().Which.Status.Should().Be(WorkflowExecutionStatus.Pending);
        waiting.StepExecutions.Single().WaitUntilUtc.Should().BeAfter(DateTime.UtcNow);
        (await _store.ClaimNextExecutionAsync("early-worker", TimeSpan.FromMinutes(1))).Should().BeNull();

        await Task.Delay(350);
        await ProcessClaimedExecutionAsync(started.Id);
        var completed = await _engine.GetExecutionAsync(TenantId, started.Id);
        completed!.Status.Should().Be(WorkflowExecutionStatus.Completed);
    }

    [Fact]
    public async Task SubworkflowStep_FailsInsteadOfReturningFalseSuccess()
    {
        var definition = new WorkflowDefinition
        {
            Id = "wf-subworkflow",
            Name = "Subworkflow",
            Steps = [new WorkflowStep { Id = "child", Name = "Child", StepType = WorkflowStepType.Subworkflow }]
        };

        var started = await _engine.StartAsync(TenantId, definition);
        await ProcessClaimedExecutionAsync(started.Id);

        var completed = await _engine.GetExecutionAsync(TenantId, started.Id);
        completed!.Status.Should().Be(WorkflowExecutionStatus.Failed);
        completed.StepExecutions.Should().ContainSingle().Which.ErrorMessage.Should().Contain("not supported");
    }

    [Fact]
    public async Task StartAsync_WithParallelSteps_ShouldExecuteInParallel()
    {
        // Arrange
        var definition = new WorkflowDefinition
        {
            Id = "wf-parallel",
            Name = "Parallel Workflow",
            Steps = new List<WorkflowStep>
            {
                new() 
                { 
                    Id = "p-1", 
                    Name = "Parallel Parent", 
                    StepType = WorkflowStepType.Parallel,
                    ParallelSteps = new List<WorkflowStep>
                    {
                        new() { Id = "sub-1", Name = "Sub 1", AgentName = "A1" },
                        new() { Id = "sub-2", Name = "Sub 2", AgentName = "A2" }
                    }
                }
            }
        };

        _agentExecutor.ExecuteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<UserContext>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(async _ => 
            {
                await Task.Delay(100);
                return new AgentResponse { Success = true, Content = "Parallel Done" };
            });

        await _store.SaveDefinitionAsync(TenantId, definition);

        // Act
        var execution = await _engine.StartAsync(TenantId, definition, initiatedBy: "user-1");
        await ProcessClaimedExecutionAsync(execution.Id);

        // Assert
        var finalState = await _engine.GetExecutionAsync(TenantId, execution.Id);
        finalState!.Status.Should().Be(WorkflowExecutionStatus.Completed);
        finalState.StepExecutions.Should().HaveCount(3); // 1 parent + 2 parallel sub-steps
        await _agentExecutor.Received(2).ExecuteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<UserContext>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartAsync_WithFailedStep_ShouldRunCompensation()
    {
        // Arrange
        var compensationStep = new WorkflowStep { Id = "comp-1", Name = "Undo Action", AgentName = "Cleaner" };
        var definition = new WorkflowDefinition
        {
            Id = "wf-fail",
            Name = "Fail Workflow",
            Steps = new List<WorkflowStep>
            {
                new() 
                { 
                    Id = "bad-step", 
                    Name = "Failing Step", 
                    AgentName = "AgentX", 
                    CompensationStep = compensationStep 
                }
            }
        };

        _agentExecutor.ExecuteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<UserContext>(), "AgentX", Arg.Any<CancellationToken>())
            .Returns(new AgentResponse { Success = false, ErrorMessage = "Boom" });

        _agentExecutor.ExecuteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<UserContext>(), "Cleaner", Arg.Any<CancellationToken>())
            .Returns(new AgentResponse { Success = true, Content = "Cleaned" });

        await _store.SaveDefinitionAsync(TenantId, definition);

        // Act
        var execution = await _engine.StartAsync(TenantId, definition, initiatedBy: "user-1");
        await ProcessClaimedExecutionAsync(execution.Id);

        // Assert
        var finalState = await _engine.GetExecutionAsync(TenantId, execution.Id);
        finalState!.Status.Should().Be(WorkflowExecutionStatus.Failed);
        var badStep = finalState.StepExecutions.First(s => s.StepId == "bad-step");
        badStep.Status.Should().Be(WorkflowExecutionStatus.Failed);
        badStep.CompensationExecuted.Should().BeTrue();
        await _agentExecutor.Received(1).ExecuteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<UserContext>(), "Cleaner", Arg.Any<CancellationToken>());
    }

    private async Task ProcessClaimedExecutionAsync(string executionId)
    {
        var claim = await _store.ClaimNextExecutionAsync("workflow-test-worker", TimeSpan.FromMinutes(1));
        claim.Should().NotBeNull();
        claim!.ExecutionId.Should().Be(executionId);
        try
        {
            await _engine.ProcessClaimedExecutionAsync(claim);
        }
        finally
        {
            await _store.ReleaseExecutionLeaseAsync(claim);
        }
    }
}
