using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Tests;

public class ToolAIFunctionFactoryTests
{
    [Fact]
    public void CreateFromTools_ReturnsEmptyList_WhenNoTools()
    {
        var loggerFactory = Substitute.For<ILoggerFactory>();
        loggerFactory.CreateLogger(Arg.Any<string>()).Returns(Substitute.For<ILogger>());

        var result = ToolAIFunctionFactory.CreateFromTools(Array.Empty<ITool>(), loggerFactory);

        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public void CreateFromTools_ReturnsOneAITool_PerITool()
    {
        var loggerFactory = Substitute.For<ILoggerFactory>();
        loggerFactory.CreateLogger(Arg.Any<string>()).Returns(Substitute.For<ILogger>());

        var tool1 = CreateMockTool("tool-1", "Tool One", "First tool");
        var tool2 = CreateMockTool("tool-2", "Tool Two", "Second tool");

        var result = ToolAIFunctionFactory.CreateFromTools(new[] { tool1, tool2 }, loggerFactory);

        result.Should().HaveCount(2);
    }

    [Fact]
    public void CreateFromTools_SetsNameFromToolId()
    {
        var loggerFactory = Substitute.For<ILoggerFactory>();
        loggerFactory.CreateLogger(Arg.Any<string>()).Returns(Substitute.For<ILogger>());

        var tool = CreateMockTool("my-tool-id", "MyTool", "Tool description");

        var result = ToolAIFunctionFactory.CreateFromTools(new[] { tool }, loggerFactory);

        var aiFunction = result[0] as AIFunction;
        aiFunction.Should().NotBeNull();
        aiFunction!.Name.Should().Be("my-tool-id");
        aiFunction.Description.Should().Be("Tool description");
    }

    [Fact]
    public void CreateFromTools_PreservesToolOrder()
    {
        var loggerFactory = Substitute.For<ILoggerFactory>();
        loggerFactory.CreateLogger(Arg.Any<string>()).Returns(Substitute.For<ILogger>());

        var tools = Enumerable.Range(1, 5)
            .Select(i => CreateMockTool($"tool-{i}", $"Tool {i}", $"Desc {i}"))
            .ToArray();

        var result = ToolAIFunctionFactory.CreateFromTools(tools, loggerFactory);

        result.Should().HaveCount(5);
        for (int i = 0; i < 5; i++)
        {
            (result[i] as AIFunction)!.Name.Should().Be($"tool-{i + 1}");
        }
    }

    private static ITool CreateMockTool(string id, string name, string description)
    {
        var tool = Substitute.For<ITool>();
        tool.Id.Returns(id);
        tool.Name.Returns(name);
        tool.Description.Returns(description);
        tool.ExecuteAsync(Arg.Any<ToolInput>(), Arg.Any<CancellationToken>())
            .Returns(ToolResult.Ok(new { status = "ok" }));
        return tool;
    }
}
