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
    private readonly DefaultWorkflowEngine _engine;
    private const string TenantId = "default";

    public WorkflowEngineTests()
    {
        _store = new InMemoryWorkflowStore();
        _agentExecutor = Substitute.For<IDirectAgentRequestExecutor>();
        _toolManager = Substitute.For<IToolManager>();
        _engine = new DefaultWorkflowEngine(
            _store,
            _agentExecutor,
            _toolManager,
            Substitute.For<ILogger<DefaultWorkflowEngine>>());
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
        execution.Status.Should().Be(WorkflowExecutionStatus.Running);
        execution.WorkflowId.Should().Be("wf-1");

        // Give it some time to process background tasks
        await Task.Delay(500);

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
        await Task.Delay(100);

        var finalState = await _engine.GetExecutionAsync(TenantId, started.Id);
        finalState!.Status.Should().Be(WorkflowExecutionStatus.Completed);
        finalState.StepExecutions.Should().ContainSingle()
            .Which.Output["content"].Should().Be("Research complete");
        await _agentExecutor.Received(1).ExecuteAsync(
            started.Id,
            "Research the topic",
            Arg.Any<UserContext>(),
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
        await Task.Delay(100);

        var waiting = await _engine.GetExecutionAsync(TenantId, started.Id);
        waiting!.Status.Should().Be(WorkflowExecutionStatus.WaitingForApproval);
        waiting.StepExecutions.Should().ContainSingle(step => step.StepId == "approval"
            && step.Status == WorkflowExecutionStatus.WaitingForApproval);
        await _agentExecutor.DidNotReceive().ExecuteAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<UserContext>(), Arg.Any<string>(), Arg.Any<CancellationToken>());

        await _engine.ApproveAsync(TenantId, started.Id, "reviewer-1");
        await Task.Delay(100);

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
        await Task.Delay(100);

        var rejected = await _engine.RejectAsync(TenantId, started.Id, "reviewer-1", "Needs revision");

        rejected.Status.Should().Be(WorkflowExecutionStatus.Cancelled);
        rejected.ErrorMessage.Should().Be("Needs revision");
        rejected.StepExecutions.Single(step => step.StepId == "approval").Status.Should().Be(WorkflowExecutionStatus.Failed);
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
            Steps = [new WorkflowStep { Id = "wait", Name = "Wait", StepType = WorkflowStepType.Wait, Timeout = TimeSpan.FromSeconds(2) }]
        };

        var started = await _engine.StartAsync(TenantId, definition);
        await Task.Delay(50);

        var waiting = await _engine.GetExecutionAsync(TenantId, started.Id);
        waiting!.Status.Should().Be(WorkflowExecutionStatus.Running);
        waiting.StepExecutions.Should().ContainSingle().Which.Status.Should().Be(WorkflowExecutionStatus.Running);

        await Task.Delay(2200);
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
        await Task.Delay(100);

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
        var execution = await _engine.StartAsync(TenantId, definition);
        await Task.Delay(1000);

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
        var execution = await _engine.StartAsync(TenantId, definition);
        await Task.Delay(500);

        // Assert
        var finalState = await _engine.GetExecutionAsync(TenantId, execution.Id);
        finalState!.Status.Should().Be(WorkflowExecutionStatus.Failed);
        var badStep = finalState.StepExecutions.First(s => s.StepId == "bad-step");
        badStep.Status.Should().Be(WorkflowExecutionStatus.Failed);
        badStep.CompensationExecuted.Should().BeTrue();
        await _agentExecutor.Received(1).ExecuteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<UserContext>(), "Cleaner", Arg.Any<CancellationToken>());
    }
}
