using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Core.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

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
    public async Task ExecuteToolAsync_WithExplicitVersion_UsesRequestedVariant()
    {
        _sut.RegisterToolVariant("search", new FakeTool("search-v1", "v1"), "1.0.0", isDefault: true);
        _sut.RegisterToolVariant("search", new FakeTool("search-v2", "v2"), "2.0.0", variantName: "beta", rolloutPercentage: 0);

        var result = await _sut.ExecuteToolAsync("search", new ToolInput
        {
            Action = "get",
            Parameters = new Dictionary<string, object>
            {
                ["toolVersion"] = "2.0.0"
            },
            UserId = "user-1"
        });

        result.Success.Should().BeTrue();
        result.Data.Should().Be("v2");
        result.Metadata.Should().ContainKey("toolVersion");
        result.Metadata!["toolVersion"].Should().Be("2.0.0");
    }

    [Fact]
    public async Task ExecuteToolAsync_WithRolloutCandidate_UsesExperimentalVariantWhenBucketMatches()
    {
        _sut.RegisterToolVariant("search", new FakeTool("search-v1", "stable"), "1.0.0", isDefault: true);
        _sut.RegisterToolVariant("search", new FakeTool("search-v2", "experiment"), "2.0.0", variantName: "experiment", rolloutPercentage: 100);

        var result = await _sut.ExecuteToolAsync("search", new ToolInput
        {
            Action = "get",
            UserId = "user-42"
        });

        result.Success.Should().BeTrue();
        result.Data.Should().Be("experiment");
        result.Metadata!["toolVariant"].Should().Be("experiment");
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
        tool.Description.Returns(name);
        tool.Category.Returns(category);
        tool.RequiresAuth.Returns(false);
        tool.IsAvailableAsync(Arg.Any<CancellationToken>()).Returns(true);
        tool.ExecuteAsync(Arg.Any<ToolInput>(), Arg.Any<CancellationToken>())
            .Returns(ToolResult.Ok(name));
        return tool;
    }

    private sealed class FakeTool(string id, string payload) : ITool
    {
        public string Id => id;
        public string Name => id;
        public string Description => id;
        public ToolCategory Category => ToolCategory.Search;
        public bool RequiresAuth => false;

        public Task<ToolResult> ExecuteAsync(ToolInput input, CancellationToken ct = default)
            => Task.FromResult(ToolResult.Ok(payload));

        public Task<bool> IsAvailableAsync(CancellationToken ct = default)
            => Task.FromResult(true);
    }
}
