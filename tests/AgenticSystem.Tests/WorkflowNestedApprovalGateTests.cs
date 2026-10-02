using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgenticSystem.Tests;

public sealed class WorkflowNestedApprovalGateTests
{
    [Fact]
    public async Task NestedParallelApproval_DoesNotAllowDependentUntilChildrenFinish()
    {
        var store = new InMemoryWorkflowStore();
        var executor = Substitute.For<IDirectAgentRequestExecutor>();
        executor.ExecuteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<UserContext>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(AgentResponse.Ok("done", "worker", AgentTier.Support));
        var engine = new DefaultWorkflowEngine(store, executor, Substitute.For<IToolManager>(), NullLogger<DefaultWorkflowEngine>.Instance);
        var definition = new WorkflowDefinition { Steps = [
            new WorkflowStep { Id = "parallel", StepType = WorkflowStepType.Parallel, ParallelSteps = [
                new WorkflowStep { Id = "approval", StepType = WorkflowStepType.Approval },
                new WorkflowStep { Id = "waiting", StepType = WorkflowStepType.Wait, Timeout = TimeSpan.FromMinutes(1) }
            ] },
            new WorkflowStep { Id = "dependent", StepType = WorkflowStepType.Agent, AgentName = "after", DependsOn = ["parallel"] }
        ] };
        var started = await engine.StartAsync("tenant", definition, initiatedBy: "owner");
        await Process(store, engine);
        await engine.ApproveStepAsync("tenant", started.Id, "approval", "owner");
        var claim = await store.ClaimNextExecutionAsync("worker", TimeSpan.FromMinutes(1));
        if (claim is not null)
        {
            await engine.ProcessClaimedExecutionAsync(claim);
            await store.ReleaseExecutionLeaseAsync(claim);
        }
        var execution = await store.GetExecutionAsync("tenant", started.Id);
        execution!.StepExecutions.Single(s => s.StepId == "parallel").Status.Should().NotBe(WorkflowExecutionStatus.Completed);
        await executor.DidNotReceiveWithAnyArgs().ExecuteAsync(default!, default!, default!, default!, default);
        execution.Status.Should().NotBe(WorkflowExecutionStatus.Completed);
    }

    [Fact]
    public async Task NestedParallelDependencies_DoNotRunChildBeforeApproval()
    {
        var store = new InMemoryWorkflowStore();
        var executor = Substitute.For<IDirectAgentRequestExecutor>();
        executor.ExecuteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<UserContext>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(AgentResponse.Ok("done", "worker", AgentTier.Support));
        var engine = new DefaultWorkflowEngine(store, executor, Substitute.For<IToolManager>(), NullLogger<DefaultWorkflowEngine>.Instance);
        var started = await engine.StartAsync("tenant", new WorkflowDefinition { Steps = [
            new WorkflowStep { Id = "parallel", StepType = WorkflowStepType.Parallel, ParallelSteps = [
                new WorkflowStep { Id = "approval", StepType = WorkflowStepType.Approval },
                new WorkflowStep { Id = "child", StepType = WorkflowStepType.Agent, AgentName = "after", DependsOn = ["approval"] }
            ] }
        ] }, initiatedBy: "owner");
        await Process(store, engine);
        await executor.DidNotReceiveWithAnyArgs().ExecuteAsync(default!, default!, default!, default!, default);
        await engine.ApproveStepAsync("tenant", started.Id, "approval", "owner");
        await Process(store, engine);
        await executor.Received(1).ExecuteAsync(started.Id, Arg.Any<string>(), Arg.Any<UserContext>(), "after", Arg.Any<CancellationToken>());
        (await store.GetExecutionAsync("tenant", started.Id))!.Status.Should().Be(WorkflowExecutionStatus.Completed);
    }

    [Fact]
    public void EdgeWithoutMatchingDependsOn_IsRejectedInsteadOfIgnoredAtRuntime()
    {
        var definition = new WorkflowDefinition { Steps = [
            new WorkflowStep { Id = "a", StepType = WorkflowStepType.Approval },
            new WorkflowStep { Id = "b", StepType = WorkflowStepType.Agent, AgentName = "after" }
        ], Edges = [new WorkflowEdge { FromStepId = "a", ToStepId = "b" }] };
        Action validate = () => WorkflowGraphValidator.Validate(definition);
        validate.Should().Throw<ArgumentException>();
    }

    private static async Task Process(IWorkflowStore store, IWorkflowEngine engine)
    {
        var claim = await store.ClaimNextExecutionAsync("worker", TimeSpan.FromMinutes(1));
        claim.Should().NotBeNull();
        try { await engine.ProcessClaimedExecutionAsync(claim!); }
        finally { await store.ReleaseExecutionLeaseAsync(claim!); }
    }
}
