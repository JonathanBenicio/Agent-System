using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Core.Services;

namespace AgenticSystem.Tests;

public class ScheduledTaskHostedServiceTests
{
    private readonly IScheduledTaskManager _taskManager = Substitute.For<IScheduledTaskManager>();
    private readonly ITriggerEngine _triggerEngine = Substitute.For<ITriggerEngine>();
    private readonly ILogger<ScheduledTaskHostedService> _logger = Substitute.For<ILogger<ScheduledTaskHostedService>>();

    private ScheduledTaskHostedService CreateService()
        => new(_taskManager, _triggerEngine, _logger);

    [Fact]
    public async Task StartAsync_DoesNotThrow()
    {
        var service = CreateService();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        var act = () => service.StartAsync(cts.Token);

        await act.Should().NotThrowAsync();
        await service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ExecuteAsync_CallsGetActiveAsync()
    {
        _taskManager.GetActiveAsync(Arg.Any<CancellationToken>())
            .Returns(new List<ScheduledTask>());

        var service = CreateService();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        await service.StartAsync(cts.Token);
        await Task.Delay(100);
        await service.StopAsync(CancellationToken.None);

        await _taskManager.Received().GetActiveAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_ExecutesDueTask()
    {
        var dueTask = new ScheduledTask
        {
            Id = "task-1",
            Name = "Test Task",
            Status = ScheduledTaskStatus.Active,
            NextRunAt = DateTime.UtcNow.AddMinutes(-1)
        };

        _taskManager.GetActiveAsync(Arg.Any<CancellationToken>())
            .Returns(new List<ScheduledTask> { dueTask });
        _taskManager.ExecuteAsync("task-1", Arg.Any<CancellationToken>())
            .Returns(new TaskExecution { TaskId = "task-1", Success = true });

        var service = CreateService();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        await service.StartAsync(cts.Token);
        await Task.Delay(100);
        await service.StopAsync(CancellationToken.None);

        await _taskManager.Received().ExecuteAsync("task-1", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_SkipsFutureTask()
    {
        var futureTask = new ScheduledTask
        {
            Id = "task-future",
            Name = "Future Task",
            Status = ScheduledTaskStatus.Active,
            NextRunAt = DateTime.UtcNow.AddHours(1)
        };

        _taskManager.GetActiveAsync(Arg.Any<CancellationToken>())
            .Returns(new List<ScheduledTask> { futureTask });

        var service = CreateService();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        await service.StartAsync(cts.Token);
        await Task.Delay(100);
        await service.StopAsync(CancellationToken.None);

        await _taskManager.DidNotReceive().ExecuteAsync("task-future", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_HandlesExecutionException_Gracefully()
    {
        var dueTask = new ScheduledTask
        {
            Id = "task-fail",
            Name = "Fail Task",
            Status = ScheduledTaskStatus.Active,
            NextRunAt = DateTime.UtcNow.AddMinutes(-1)
        };

        _taskManager.GetActiveAsync(Arg.Any<CancellationToken>())
            .Returns(new List<ScheduledTask> { dueTask });
        _taskManager.ExecuteAsync("task-fail", Arg.Any<CancellationToken>())
            .Returns<TaskExecution>(x => throw new InvalidOperationException("execution failed"));

        var service = CreateService();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        await service.StartAsync(cts.Token);
        await Task.Delay(100);
        await service.StopAsync(CancellationToken.None);

        // Should not throw — service stays alive
        await _taskManager.Received().ExecuteAsync("task-fail", Arg.Any<CancellationToken>());
    }
}
