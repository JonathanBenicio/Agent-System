using System.Text.Json;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgenticSystem.Tests;

public sealed class WorkflowReviewRegressionTests
{
    [Theory]
    [InlineData(WorkflowStepType.Action, "action")]
    [InlineData(WorkflowStepType.Agent, "agent")]
    [InlineData(WorkflowStepType.Decision, "decision")]
    [InlineData(WorkflowStepType.Parallel, "parallel")]
    [InlineData(WorkflowStepType.Wait, "wait")]
    [InlineData(WorkflowStepType.Approval, "approval")]
    [InlineData(WorkflowStepType.Subworkflow, "subworkflow")]
    public void StepTypeRoundTripUsesStableNameAndAcceptsSnapshotNumber(WorkflowStepType type, string name)
    {
        JsonSerializer.Serialize(type).Should().Be($"\"{name}\"");
        JsonSerializer.Deserialize<WorkflowStepType>($"\"{name}\"").Should().Be(type);
        JsonSerializer.Deserialize<WorkflowStepType>(((int)type).ToString()).Should().Be(type);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("cycle")]
    [InlineData("self")]
    public async Task SchedulerDependenciesInvalid_AreRejectedBeforePersistence(string invalid)
    {
        var store = Substitute.For<IWorkflowStore>();
        var engine = Engine(store, Substitute.For<IDirectAgentRequestExecutor>());
        var first = new WorkflowStep { Id = "a", DependsOn = [invalid == "missing" ? "missing" : invalid == "self" ? "a" : "b"] };
        var definition = new WorkflowDefinition { Steps = [first, new WorkflowStep { Id = "b", DependsOn = ["a"] }] };
        await ((Func<Task>)(() => engine.StartAsync("t", definition))).Should().ThrowAsync<Exception>();
        await store.DidNotReceiveWithAnyArgs().SaveExecutionAsync(default!, default!, default);
    }

    [Fact]
    public async Task TwoApprovalsAreSelectedIndependentlyAndLegacyRequestIsAmbiguous()
    {
        var store = new InMemoryWorkflowStore();
        var engine = Engine(store, Substitute.For<IDirectAgentRequestExecutor>());
        var definition = new WorkflowDefinition { Steps = [
            new WorkflowStep { Id = "approve-a", StepType = WorkflowStepType.Approval },
            new WorkflowStep { Id = "approve-b", StepType = WorkflowStepType.Approval }
        ] };
        var execution = await engine.StartAsync("t", definition, initiatedBy: "owner");
        await Process(store, engine);
        var waiting = await engine.GetExecutionAsync("t", execution.Id);
        waiting!.StepExecutions.Count(step => step.Status == WorkflowExecutionStatus.WaitingForApproval).Should().Be(2);
        await ((Func<Task>)(() => engine.ApproveAsync("t", execution.Id, "owner")))
            .Should().ThrowAsync<WorkflowApprovalAmbiguousException>();
        (await engine.ApproveStepAsync("t", execution.Id, "approve-a", "owner")).Status
            .Should().Be(WorkflowExecutionStatus.WaitingForApproval);
        (await engine.ApproveStepAsync("t", execution.Id, "approve-b", "owner")).Status
            .Should().Be(WorkflowExecutionStatus.Pending);
        await Process(store, engine);
        (await engine.GetExecutionAsync("t", execution.Id))!.Status.Should().Be(WorkflowExecutionStatus.Completed);
    }

    [Fact]
    public async Task ParallelStepsOverlapButMergeOutputsDeterministicallyWithoutConcurrentPersistence()
    {
        var store = new InMemoryWorkflowStore();
        var executor = Substitute.For<IDirectAgentRequestExecutor>();
        var allStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var count = 0;
        executor.ExecuteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<UserContext>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                if (Interlocked.Increment(ref count) == 8) allStarted.TrySetResult();
                await allStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
                return AgentResponse.Ok(call.ArgAt<string>(3), call.ArgAt<string>(3), AgentTier.Support);
            });
        var engine = Engine(store, executor);
        var definition = new WorkflowDefinition { Steps = Enumerable.Range(0, 8).Select(i => new WorkflowStep
            { Id = $"step-{i}", AgentName = $"agent-{i}", StepType = WorkflowStepType.Agent }).ToList() };
        var execution = await engine.StartAsync("t", definition, initiatedBy: "owner");
        await Process(store, engine);
        var result = await engine.GetExecutionAsync("t", execution.Id);
        result!.Status.Should().Be(WorkflowExecutionStatus.Completed);
        result.StepExecutions.Should().HaveCount(8);
        for (var i = 0; i < 8; i++) result.Variables[$"step-{i}.content"].ToString().Should().Be($"agent-{i}");
        result.Variables["content"].ToString().Should().Be("agent-0");
    }

    private static DefaultWorkflowEngine Engine(IWorkflowStore store, IDirectAgentRequestExecutor executor)
        => new(store, executor, Substitute.For<IToolManager>(), NullLogger<DefaultWorkflowEngine>.Instance);

    private static async Task Process(IWorkflowStore store, IWorkflowEngine engine)
    {
        var claim = await store.ClaimNextExecutionAsync("review-worker", TimeSpan.FromMinutes(1));
        claim.Should().NotBeNull();
        try { await engine.ProcessClaimedExecutionAsync(claim!); }
        finally { await store.ReleaseExecutionLeaseAsync(claim!); }
    }
}
