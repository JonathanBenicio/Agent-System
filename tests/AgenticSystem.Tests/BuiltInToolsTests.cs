using FluentAssertions;
using AgenticSystem.Core.Tools;
using AgenticSystem.Core.Interfaces;

namespace AgenticSystem.Tests;

public class BuiltInToolsTests
{
    [Fact]
    public async Task DateTimeTool_Now_ReturnsCurrentTime()
    {
        var tool = new DateTimeTool();
        var input = new ToolInput { Action = "now" };

        var result = await tool.ExecuteAsync(input);

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
    }

    [Fact]
    public async Task DateTimeTool_Diff_CalculatesDifference()
    {
        var tool = new DateTimeTool();
        var input = new ToolInput
        {
            Action = "diff",
            Parameters = new Dictionary<string, object> { ["date"] = "2020-01-01" }
        };

        var result = await tool.ExecuteAsync(input);

        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task DateTimeTool_Diff_WithInvalidDate_ReturnsFail()
    {
        var tool = new DateTimeTool();
        var input = new ToolInput
        {
            Action = "diff",
            Parameters = new Dictionary<string, object> { ["date"] = "not-a-date" }
        };

        var result = await tool.ExecuteAsync(input);

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task CalculatorTool_BasicOperation_ReturnsResult()
    {
        var tool = new CalculatorTool();
        var input = new ToolInput
        {
            Action = "calculate",
            Parameters = new Dictionary<string, object> { ["expression"] = "2+3" }
        };

        var result = await tool.ExecuteAsync(input);

        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task CalculatorTool_MissingExpression_ReturnsFail()
    {
        var tool = new CalculatorTool();
        var input = new ToolInput { Action = "calculate" };

        var result = await tool.ExecuteAsync(input);

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task FileSearchTool_List_WithInvalidPath_ReturnsFail()
    {
        var tool = new FileSearchTool();
        var input = new ToolInput
        {
            Action = "list",
            Parameters = new Dictionary<string, object> { ["path"] = "Z:\\nonexistent\\path" }
        };

        var result = await tool.ExecuteAsync(input);

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task DateTimeTool_IsAvailable_ReturnsTrue()
    {
        var tool = new DateTimeTool();
        var available = await tool.IsAvailableAsync();
        available.Should().BeTrue();
    }

    [Fact]
    public async Task CalculatorTool_Properties_AreCorrect()
    {
        var tool = new CalculatorTool();
        tool.Id.Should().Be("calculator");
        tool.Category.Should().Be(ToolCategory.Tasks);
        tool.RequiresAuth.Should().BeFalse();
    }

    [Fact]
    public async Task FileSearchTool_Properties_AreCorrect()
    {
        var tool = new FileSearchTool();
        tool.Id.Should().Be("file-search");
        tool.Category.Should().Be(ToolCategory.Storage);
    }
}
