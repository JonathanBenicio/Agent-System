using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using AIChatResponse = Microsoft.Extensions.AI.ChatResponse;

namespace AgenticSystem.Tests;

public class ChatClientPlannerTests
{
    private readonly IChatClient _chatClient;
    private readonly ITaskPlanManager _taskPlanManager;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IToolManager _toolManager;

    public ChatClientPlannerTests()
    {
        _chatClient = Substitute.For<IChatClient>();
        _taskPlanManager = Substitute.For<ITaskPlanManager>();
        _loggerFactory = Substitute.For<ILoggerFactory>();
        _loggerFactory.CreateLogger(Arg.Any<string>()).Returns(Substitute.For<ILogger>());
        _toolManager = Substitute.For<IToolManager>();
    }

    [Fact]
    public void Constructor_ThrowsOnNullChatClient()
    {
        var act = () => new ChatClientPlanner(null!, _taskPlanManager, _loggerFactory);
        act.Should().Throw<ArgumentNullException>().WithParameterName("chatClient");
    }

    [Fact]
    public void Constructor_ThrowsOnNullTaskPlanManager()
    {
        var act = () => new ChatClientPlanner(_chatClient, null!, _loggerFactory);
        act.Should().Throw<ArgumentNullException>().WithParameterName("taskPlanManager");
    }

    [Fact]
    public void Constructor_ThrowsOnNullLoggerFactory()
    {
        var act = () => new ChatClientPlanner(_chatClient, _taskPlanManager, null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("loggerFactory");
    }

    [Fact]
    public void Constructor_AcceptsNullToolManager()
    {
        var act = () => new ChatClientPlanner(_chatClient, _taskPlanManager, _loggerFactory, toolProvider: null);
        act.Should().NotThrow();
    }

    [Fact]
    public async Task PlanAsync_ReturnsNull_WhenChatClientReturnsEmpty()
    {
        var response = new AIChatResponse(new ChatMessage(ChatRole.Assistant, ""));
        _chatClient.GetResponseAsync(
            Arg.Any<IEnumerable<ChatMessage>>(),
            Arg.Any<ChatOptions>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(response));

        var sut = new ChatClientPlanner(_chatClient, _taskPlanManager, _loggerFactory);
        var result = await sut.PlanAsync("user1", "do something");

        result.Should().BeNull();
    }

    [Fact]
    public async Task PlanAsync_ParsesJsonSteps_AndCreatesPlan()
    {
        var jsonSteps = """[{"description":"Step 1","agent":"Analysis"},{"description":"Step 2","agent":"Work"}]""";
        var response = new AIChatResponse(new ChatMessage(ChatRole.Assistant, jsonSteps));
        _chatClient.GetResponseAsync(
            Arg.Any<IEnumerable<ChatMessage>>(),
            Arg.Any<ChatOptions>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(response));

        var expectedPlan = new TaskPlan
        {
            UserId = "user1",
            Title = "do something",
            Steps = new List<TaskStep>
            {
                new() { Index = 0, Description = "Step 1", AssignedAgent = "Analysis" },
                new() { Index = 1, Description = "Step 2", AssignedAgent = "Work" }
            }
        };
        _taskPlanManager.CreatePlanAsync("user1", "do something", Arg.Any<List<TaskStep>>())
            .Returns(expectedPlan);

        var sut = new ChatClientPlanner(_chatClient, _taskPlanManager, _loggerFactory);
        var result = await sut.PlanAsync("user1", "do something");

        result.Should().NotBeNull();
        result!.Steps.Should().HaveCount(2);
        await _taskPlanManager.Received(1).CreatePlanAsync("user1", "do something", Arg.Is<List<TaskStep>>(
            s => s.Count == 2 && s[0].Description == "Step 1" && s[1].AssignedAgent == "Work"));
    }

    [Fact]
    public async Task PlanAsync_ParsesJsonWrappedInText()
    {
        var content = """Here are the steps: [{"description":"Do X","agent":"API"}] Hope that helps!""";
        var response = new AIChatResponse(new ChatMessage(ChatRole.Assistant, content));
        _chatClient.GetResponseAsync(
            Arg.Any<IEnumerable<ChatMessage>>(),
            Arg.Any<ChatOptions>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(response));

        _taskPlanManager.CreatePlanAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<List<TaskStep>>())
            .Returns(new TaskPlan { Steps = new List<TaskStep> { new() { Description = "Do X" } } });

        var sut = new ChatClientPlanner(_chatClient, _taskPlanManager, _loggerFactory);
        var result = await sut.PlanAsync("user1", "objective");

        result.Should().NotBeNull();
        await _taskPlanManager.Received(1).CreatePlanAsync("user1", "objective",
            Arg.Is<List<TaskStep>>(s => s.Count == 1 && s[0].Description == "Do X"));
    }

    [Fact]
    public async Task PlanAsync_FallsBackToLineParsing_WhenJsonInvalid()
    {
        var content = "- First step\n- Second step\n- Third step";
        var response = new AIChatResponse(new ChatMessage(ChatRole.Assistant, content));
        _chatClient.GetResponseAsync(
            Arg.Any<IEnumerable<ChatMessage>>(),
            Arg.Any<ChatOptions>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(response));

        _taskPlanManager.CreatePlanAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<List<TaskStep>>())
            .Returns(new TaskPlan { Steps = new List<TaskStep> { new(), new(), new() } });

        var sut = new ChatClientPlanner(_chatClient, _taskPlanManager, _loggerFactory);
        var result = await sut.PlanAsync("user1", "objective");

        result.Should().NotBeNull();
        await _taskPlanManager.Received(1).CreatePlanAsync("user1", "objective",
            Arg.Is<List<TaskStep>>(s => s.Count == 3));
    }

    [Fact]
    public async Task PlanAsync_InjectsToolsFromToolManager()
    {
        var tool = Substitute.For<ITool>();
        tool.Id.Returns("calendar");
        tool.Name.Returns("Calendar");
        tool.Description.Returns("Calendar tool");
        _toolManager.GetAvailableToolsAsync(null).Returns(new[] { tool });

        var jsonSteps = """[{"description":"Check calendar","agent":"Calendar"}]""";
        var response = new AIChatResponse(new ChatMessage(ChatRole.Assistant, jsonSteps));
        _chatClient.GetResponseAsync(
            Arg.Any<IEnumerable<ChatMessage>>(),
            Arg.Any<ChatOptions>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(response));

        _taskPlanManager.CreatePlanAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<List<TaskStep>>())
            .Returns(new TaskPlan());

        var toolProvider = new UnifiedAIToolProvider(_loggerFactory, _toolManager);
        var sut = new ChatClientPlanner(_chatClient, _taskPlanManager, _loggerFactory, toolProvider);
        await sut.PlanAsync("user1", "check my schedule");

        await _toolManager.Received(1).GetAvailableToolsAsync(null);
    }
}
