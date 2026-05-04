using FluentAssertions;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Core.Services;

namespace AgenticSystem.Tests;

public class InMemoryScheduledTaskStoreTests
{
    private readonly InMemoryScheduledTaskStore _sut;

    public InMemoryScheduledTaskStoreTests()
    {
        _sut = new InMemoryScheduledTaskStore();
    }

    [Fact]
    public async Task SaveTaskAsync_NewTask_PersistsAndReturns()
    {
        var task = new ScheduledTask { Id = "t1", Name = "test-task" };

        var result = await _sut.SaveTaskAsync(task);

        result.Should().Be(task);
        var stored = await _sut.GetTaskAsync("t1");
        stored.Should().Be(task);
    }

    [Fact]
    public async Task SaveTaskAsync_ExistingTask_Updates()
    {
        var task = new ScheduledTask { Id = "t1", Name = "original" };
        await _sut.SaveTaskAsync(task);
        task.Name = "updated";
        await _sut.SaveTaskAsync(task);

        var stored = await _sut.GetTaskAsync("t1");
        stored!.Name.Should().Be("updated");
    }

    [Fact]
    public async Task GetTaskAsync_NonExistent_ReturnsNull()
    {
        var result = await _sut.GetTaskAsync("missing");
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAllTasksAsync_MultipleTasks_ReturnsAll()
    {
        await _sut.SaveTaskAsync(new ScheduledTask { Id = "t1", Name = "a" });
        await _sut.SaveTaskAsync(new ScheduledTask { Id = "t2", Name = "b" });

        var all = await _sut.GetAllTasksAsync();

        all.Should().HaveCount(2);
    }

    [Fact]
    public async Task DeleteTaskAsync_Removes()
    {
        await _sut.SaveTaskAsync(new ScheduledTask { Id = "t1", Name = "test" });

        await _sut.DeleteTaskAsync("t1");

        var result = await _sut.GetTaskAsync("t1");
        result.Should().BeNull();
    }

    [Fact]
    public async Task SaveRuleAsync_PersistsAndReturns()
    {
        var rule = new TriggerRule
        {
            Id = "r1", Name = "rule-1",
            Source = new TriggerSource(TriggerSourceType.HttpGet, "https://api.test.com", new Dictionary<string, string>()),
            Condition = new TriggerCondition(ConditionType.Contains, "ok"),
            Action = new TriggerAction("log", "Log result", new Dictionary<string, string>()),
            DeliveryChannels = new[] { "webhook" }
        };

        var result = await _sut.SaveRuleAsync(rule);

        result.Should().Be(rule);
        var stored = await _sut.GetRuleAsync("r1");
        stored.Should().Be(rule);
    }

    [Fact]
    public async Task GetAllRulesAsync_ReturnsAll()
    {
        await _sut.SaveRuleAsync(new TriggerRule
        {
            Id = "r1", Name = "a",
            Source = new TriggerSource(TriggerSourceType.HttpGet, "https://a.com", new Dictionary<string, string>()),
            Condition = new TriggerCondition(ConditionType.Contains, "x"),
            Action = new TriggerAction("log", "desc", new Dictionary<string, string>()),
            DeliveryChannels = Array.Empty<string>()
        });
        await _sut.SaveRuleAsync(new TriggerRule
        {
            Id = "r2", Name = "b",
            Source = new TriggerSource(TriggerSourceType.HttpGet, "https://b.com", new Dictionary<string, string>()),
            Condition = new TriggerCondition(ConditionType.Contains, "y"),
            Action = new TriggerAction("log", "desc", new Dictionary<string, string>()),
            DeliveryChannels = Array.Empty<string>()
        });

        var all = await _sut.GetAllRulesAsync();
        all.Should().HaveCount(2);
    }

    [Fact]
    public async Task DeleteRuleAsync_RemovesRule()
    {
        await _sut.SaveRuleAsync(new TriggerRule
        {
            Id = "r1", Name = "rule",
            Source = new TriggerSource(TriggerSourceType.HttpGet, "https://a.com", new Dictionary<string, string>()),
            Condition = new TriggerCondition(ConditionType.Contains, "x"),
            Action = new TriggerAction("log", "desc", new Dictionary<string, string>()),
            DeliveryChannels = Array.Empty<string>()
        });

        await _sut.DeleteRuleAsync("r1");

        var result = await _sut.GetRuleAsync("r1");
        result.Should().BeNull();
    }

    [Fact]
    public async Task SaveExecutionAsync_PersistsExecution()
    {
        var execution = new TaskExecution
        {
            ExecutionId = "e1",
            TaskId = "t1",
            StartedAt = DateTime.UtcNow,
            Success = true
        };

        var result = await _sut.SaveExecutionAsync(execution);

        result.Should().Be(execution);
    }
}
