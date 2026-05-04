using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Services;

namespace AgenticSystem.Tests;

public class InMemoryToolManagerTests
{
    private readonly ILogger<InMemoryToolManager> _logger;
    private readonly InMemoryToolManager _sut;

    public InMemoryToolManagerTests()
    {
        _logger = Substitute.For<ILogger<InMemoryToolManager>>();
        _sut = new InMemoryToolManager(_logger);
    }

    [Fact]
    public async Task RegisterTool_AddsToolSuccessfully()
    {
        var tool = CreateMockTool("test-tool", "Test", ToolCategory.Tasks);
        _sut.RegisterTool(tool);

        var tools = await _sut.GetAvailableToolsAsync(null);
        tools.Should().Contain(t => t.Id == "test-tool");
    }

    [Fact]
    public async Task GetAvailableToolsAsync_WithCategory_FiltersCorrectly()
    {
        _sut.RegisterTool(CreateMockTool("cal1", "Calendar 1", ToolCategory.Calendar));
        _sut.RegisterTool(CreateMockTool("task1", "Task 1", ToolCategory.Tasks));

        var calendarTools = await _sut.GetAvailableToolsAsync("Calendar");
        calendarTools.Should().HaveCount(1);
        calendarTools.First().Id.Should().Be("cal1");
    }

    [Fact]
    public async Task GetAvailableToolsAsync_WithNullCategory_ReturnsAll()
    {
        _sut.RegisterTool(CreateMockTool("t1", "Tool 1", ToolCategory.Calendar));
        _sut.RegisterTool(CreateMockTool("t2", "Tool 2", ToolCategory.Api));

        var tools = await _sut.GetAvailableToolsAsync(null);
        tools.Should().HaveCountGreaterOrEqualTo(2);
    }

    [Fact]
    public async Task ExecuteToolAsync_WithValidTool_ReturnsResult()
    {
        var tool = CreateMockTool("exec-tool", "Executable", ToolCategory.Tasks);
        tool.ExecuteAsync(Arg.Any<ToolInput>(), Arg.Any<CancellationToken>())
            .Returns(ToolResult.Ok("done"));
        tool.IsAvailableAsync(Arg.Any<CancellationToken>()).Returns(true);

        _sut.RegisterTool(tool);

        var input = new ToolInput { Action = "run", Parameters = new() };
        var result = await _sut.ExecuteToolAsync("exec-tool", input, CancellationToken.None);

        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteToolAsync_WithNonexistentTool_ReturnsFail()
    {
        var input = new ToolInput { Action = "run" };
        var result = await _sut.ExecuteToolAsync("nonexistent", input, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("nonexistent");
    }

    [Fact]
    public void UnregisterTool_RemovesExistingTool()
    {
        _sut.RegisterTool(CreateMockTool("t1", "Tool 1", ToolCategory.Tasks));

        var removed = _sut.UnregisterTool("t1");

        removed.Should().BeTrue();
        _sut.GetTool("t1").Should().BeNull();
    }

    [Fact]
    public void UnregisterTool_WhenNotExists_ReturnsFalse()
    {
        var removed = _sut.UnregisterTool("nonexistent");

        removed.Should().BeFalse();
    }

    [Fact]
    public void GetTool_WhenExists_ReturnsTool()
    {
        var tool = CreateMockTool("t1", "Tool 1", ToolCategory.Api);
        _sut.RegisterTool(tool);

        var found = _sut.GetTool("t1");

        found.Should().NotBeNull();
        found!.Id.Should().Be("t1");
    }

    [Fact]
    public void GetTool_WhenNotExists_ReturnsNull()
    {
        var found = _sut.GetTool("nonexistent");

        found.Should().BeNull();
    }

    private ITool CreateMockTool(string id, string name, ToolCategory category)
    {
        var tool = Substitute.For<ITool>();
        tool.Id.Returns(id);
        tool.Name.Returns(name);
        tool.Category.Returns(category);
        tool.RequiresAuth.Returns(false);
        tool.IsAvailableAsync(Arg.Any<CancellationToken>()).Returns(true);
        return tool;
    }
}
