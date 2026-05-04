using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Core.Services;

namespace AgenticSystem.Tests;

public class ScheduledTaskManagerTests
{
    private readonly IScheduledTaskStore _store;
    private readonly ITriggerEngine _triggerEngine;
    private readonly ILogger<ScheduledTaskManager> _logger;
    private readonly ScheduledTaskManager _sut;

    public ScheduledTaskManagerTests()
    {
        _store = Substitute.For<IScheduledTaskStore>();
        _triggerEngine = Substitute.For<ITriggerEngine>();
        _logger = Substitute.For<ILogger<ScheduledTaskManager>>();
        _sut = new ScheduledTaskManager(_store, _triggerEngine, _logger);

        _store.SaveTaskAsync(Arg.Any<ScheduledTask>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.Arg<ScheduledTask>());
        _store.SaveExecutionAsync(Arg.Any<TaskExecution>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.Arg<TaskExecution>());
    }

    [Fact]
    public async Task RegisterAsync_CronExpression_CreatesActiveTask()
    {
        var task = await _sut.RegisterAsync("health-check", "*/5 * * * *");

        task.Name.Should().Be("health-check");
        task.Schedule.Should().Be("*/5 * * * *");
        task.Status.Should().Be(ScheduledTaskStatus.Active);
        task.NextRunAt.Should().NotBeNull();
        await _store.Received(1).SaveTaskAsync(Arg.Any<ScheduledTask>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegisterAsync_TimeSpan_CreatesActiveTask()
    {
        var interval = TimeSpan.FromMinutes(10);

        var task = await _sut.RegisterAsync("metrics-poll", interval);

        task.Name.Should().Be("metrics-poll");
        task.Interval.Should().Be(interval);
        task.Status.Should().Be(ScheduledTaskStatus.Active);
        task.NextRunAt.Should().BeCloseTo(DateTime.UtcNow.Add(interval), TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task RegisterAsync_WithRule_RegistersRuleInTriggerEngine()
    {
        var rule = CreateTestRule();

        var task = await _sut.RegisterAsync("triggered-task", "*/10 * * * *", rule);

        task.AssociatedRule.Should().Be(rule);
        await _triggerEngine.Received(1).RegisterRuleAsync(rule, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PauseAsync_ExistingTask_SetsPausedStatus()
    {
        var task = new ScheduledTask { Id = "t1", Name = "test", Status = ScheduledTaskStatus.Active };
        _store.GetTaskAsync("t1", Arg.Any<CancellationToken>()).Returns(task);

        await _sut.PauseAsync("t1");

        task.Status.Should().Be(ScheduledTaskStatus.Paused);
        await _store.Received().SaveTaskAsync(task, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PauseAsync_NonExistentTask_ThrowsInvalidOperation()
    {
        _store.GetTaskAsync("missing", Arg.Any<CancellationToken>()).Returns((ScheduledTask?)null);

        var act = () => _sut.PauseAsync("missing");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ResumeAsync_PausedTask_SetsActiveAndRecalculatesNextRun()
    {
        var task = new ScheduledTask
        {
            Id = "t1", Name = "test",
            Status = ScheduledTaskStatus.Paused,
            Interval = TimeSpan.FromMinutes(5)
        };
        _store.GetTaskAsync("t1", Arg.Any<CancellationToken>()).Returns(task);

        await _sut.ResumeAsync("t1");

        task.Status.Should().Be(ScheduledTaskStatus.Active);
        task.NextRunAt.Should().BeCloseTo(DateTime.UtcNow.AddMinutes(5), TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task RemoveAsync_TaskWithRule_RemovesBothTaskAndRule()
    {
        var rule = CreateTestRule();
        var task = new ScheduledTask { Id = "t1", Name = "test", AssociatedRule = rule };
        _store.GetTaskAsync("t1", Arg.Any<CancellationToken>()).Returns(task);

        await _sut.RemoveAsync("t1");

        await _triggerEngine.Received(1).RemoveRuleAsync(rule.Id, Arg.Any<CancellationToken>());
        await _store.Received(1).DeleteTaskAsync("t1", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithRule_EvaluatesTrigger()
    {
        var rule = CreateTestRule();
        var task = new ScheduledTask
        {
            Id = "t1", Name = "test",
            Status = ScheduledTaskStatus.Active,
            AssociatedRule = rule,
            Interval = TimeSpan.FromMinutes(5)
        };
        _store.GetTaskAsync("t1", Arg.Any<CancellationToken>()).Returns(task);
        _triggerEngine.EvaluateAsync(rule, Arg.Any<CancellationToken>())
            .Returns(new TriggerEvaluationResult { RuleId = rule.Id, ConditionMet = true });

        var execution = await _sut.ExecuteAsync("t1");

        execution.Success.Should().BeTrue();
        execution.CompletedAt.Should().NotBeNull();
        task.TotalExecutions.Should().Be(1);
        await _triggerEngine.Received(1).EvaluateAsync(rule, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenTriggerThrows_RecordsFailure()
    {
        var rule = CreateTestRule();
        var task = new ScheduledTask
        {
            Id = "t1", Name = "test",
            Status = ScheduledTaskStatus.Active,
            AssociatedRule = rule,
            Interval = TimeSpan.FromMinutes(5)
        };
        _store.GetTaskAsync("t1", Arg.Any<CancellationToken>()).Returns(task);
        _triggerEngine.EvaluateAsync(rule, Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        var execution = await _sut.ExecuteAsync("t1");

        execution.Success.Should().BeFalse();
        execution.ErrorMessage.Should().Contain("Connection refused");
        task.FailedExecutions.Should().Be(1);
        task.TotalExecutions.Should().Be(1);
    }

    [Fact]
    public async Task GetActiveAsync_FiltersOnlyActiveTasks()
    {
        var tasks = new List<ScheduledTask>
        {
            new() { Id = "t1", Status = ScheduledTaskStatus.Active },
            new() { Id = "t2", Status = ScheduledTaskStatus.Paused },
            new() { Id = "t3", Status = ScheduledTaskStatus.Active }
        };
        _store.GetAllTasksAsync(Arg.Any<CancellationToken>()).Returns(tasks);

        var result = await _sut.GetActiveAsync();

        result.Should().HaveCount(2);
        result.Should().OnlyContain(t => t.Status == ScheduledTaskStatus.Active);
    }

    private static TriggerRule CreateTestRule() => new()
    {
        Id = "rule-1",
        Name = "Health Check Rule",
        Schedule = "*/5 * * * *",
        Source = new TriggerSource(TriggerSourceType.HealthCheck, "https://api.example.com/health", new Dictionary<string, string>()),
        Condition = new TriggerCondition(ConditionType.StatusCode, "200"),
        Action = new TriggerAction("notify", "Notify team on failure", new Dictionary<string, string>()),
        DeliveryChannels = new[] { "webhook" },
        Enabled = true
    };
}
