using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using AgenticSystem.Core.Models;
using AgenticSystem.Core.Services;

namespace AgenticSystem.Tests;

public class TaskPlanManagerTests
{
    private readonly TaskPlanManager _sut;

    public TaskPlanManagerTests()
    {
        var logger = Substitute.For<ILogger<TaskPlanManager>>();
        _sut = new TaskPlanManager(logger);
    }

    [Fact]
    public async Task CreatePlanAsync_ReturnsPlanWithSteps()
    {
        var steps = new List<TaskStep>
        {
            new() { Description = "First step" },
            new() { Description = "Second step" }
        };

        var plan = await _sut.CreatePlanAsync("user1", "Test Plan", steps);

        plan.Should().NotBeNull();
        plan.UserId.Should().Be("user1");
        plan.Steps.Should().HaveCount(2);
        plan.Steps[0].Index.Should().Be(0);
        plan.Steps[1].Index.Should().Be(1);
        plan.Status.Should().Be(TaskPlanStatus.Created);
    }

    [Fact]
    public async Task AdvanceStepAsync_CompletesCurrentStep()
    {
        var steps = new List<TaskStep>
        {
            new() { Description = "Step 1" },
            new() { Description = "Step 2" }
        };
        var plan = await _sut.CreatePlanAsync("user1", "Test", steps);

        await _sut.AdvanceStepAsync(plan.Id, "Done");

        plan.Steps[0].Status.Should().Be(TaskStepStatus.Completed);
        plan.Steps[0].Result.Should().Be("Done");
        plan.CurrentStepIndex.Should().Be(1);
        plan.Status.Should().Be(TaskPlanStatus.InProgress);
    }

    [Fact]
    public async Task AdvanceStepAsync_LastStep_CompletesPlan()
    {
        var steps = new List<TaskStep> { new() { Description = "Only step" } };
        var plan = await _sut.CreatePlanAsync("user1", "Test", steps);

        await _sut.AdvanceStepAsync(plan.Id);

        plan.Status.Should().Be(TaskPlanStatus.Completed);
        plan.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task FailStepAsync_FailsPlan()
    {
        var steps = new List<TaskStep> { new() { Description = "Step 1" } };
        var plan = await _sut.CreatePlanAsync("user1", "Test", steps);

        await _sut.FailStepAsync(plan.Id, "Something went wrong");

        plan.Status.Should().Be(TaskPlanStatus.Failed);
        plan.Steps[0].Status.Should().Be(TaskStepStatus.Failed);
    }

    [Fact]
    public async Task PauseAndResumePlanAsync_TogglesState()
    {
        var steps = new List<TaskStep> { new() { Description = "Step 1" }, new() { Description = "Step 2" } };
        var plan = await _sut.CreatePlanAsync("user1", "Test", steps);
        await _sut.AdvanceStepAsync(plan.Id);

        await _sut.PausePlanAsync(plan.Id);
        plan.Status.Should().Be(TaskPlanStatus.Paused);

        await _sut.ResumePlanAsync(plan.Id);
        plan.Status.Should().Be(TaskPlanStatus.InProgress);
    }

    [Fact]
    public async Task CancelPlanAsync_CancelsPendingSteps()
    {
        var steps = new List<TaskStep>
        {
            new() { Description = "Step 1" },
            new() { Description = "Step 2" }
        };
        var plan = await _sut.CreatePlanAsync("user1", "Test", steps);

        await _sut.CancelPlanAsync(plan.Id);

        plan.Status.Should().Be(TaskPlanStatus.Cancelled);
        plan.Steps.Should().AllSatisfy(s => s.Status.Should().Be(TaskStepStatus.Skipped));
    }

    [Fact]
    public async Task GetActivePlansAsync_ReturnsOnlyActiveForUser()
    {
        await _sut.CreatePlanAsync("user1", "Plan A", new List<TaskStep> { new() { Description = "S1" } });
        await _sut.CreatePlanAsync("user2", "Plan B", new List<TaskStep> { new() { Description = "S1" } });

        var plans = await _sut.GetActivePlansAsync("user1");

        plans.Should().HaveCount(1);
        plans.First().UserId.Should().Be("user1");
    }
}
