using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Services;

namespace AgenticSystem.Tests;

public class ToolAvailabilityGuardTests
{
    private readonly IToolManager _toolManager;
    private readonly IToolDiscoveryService _discoveryService;
    private readonly ILogger<ToolAvailabilityGuard> _logger;
    private readonly ToolAvailabilityGuard _sut;

    public ToolAvailabilityGuardTests()
    {
        _toolManager = Substitute.For<IToolManager>();
        _discoveryService = Substitute.For<IToolDiscoveryService>();
        _logger = Substitute.For<ILogger<ToolAvailabilityGuard>>();
        _sut = new ToolAvailabilityGuard(_toolManager, _discoveryService, _logger);

        _discoveryService.SearchAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<ToolSuggestion>());
    }

    [Fact]
    public async Task CheckAsync_EmptyList_ReturnsFullCoverage()
    {
        var result = await _sut.CheckAsync(Array.Empty<string>());

        result.AllAvailable.Should().BeTrue();
        result.MissingTools.Should().BeEmpty();
    }

    [Fact]
    public async Task CheckAsync_ChatOnly_ReturnsFullCoverage()
    {
        var result = await _sut.CheckAsync(new[] { "chat" });

        result.AllAvailable.Should().BeTrue();
        _toolManager.DidNotReceive().GetTool(Arg.Any<string>());
    }

    [Fact]
    public async Task CheckAsync_AllToolsAvailable_ReturnsFullCoverage()
    {
        SetupTool("search", available: true);
        SetupTool("github", available: true);

        var result = await _sut.CheckAsync(new[] { "search", "github" });

        result.AllAvailable.Should().BeTrue();
        result.CoverageRatio.Should().Be(1.0);
        result.AvailableTools.Should().HaveCount(2);
        result.MissingTools.Should().BeEmpty();
    }

    [Fact]
    public async Task CheckAsync_SomeToolsMissing_ReturnsPartialCoverage()
    {
        SetupTool("search", available: true);
        _toolManager.GetTool("finance-api").Returns((ITool?)null);

        var result = await _sut.CheckAsync(new[] { "search", "finance-api" });

        result.AllAvailable.Should().BeFalse();
        result.NoneAvailable.Should().BeFalse();
        result.CoverageRatio.Should().Be(0.5);
        result.AvailableTools.Should().Contain("search");
        result.MissingTools.Should().Contain("finance-api");
    }

    [Fact]
    public async Task CheckAsync_AllToolsMissing_ReturnsNoCoverage()
    {
        _toolManager.GetTool("finance-api").Returns((ITool?)null);
        _toolManager.GetTool("calendar").Returns((ITool?)null);

        var result = await _sut.CheckAsync(new[] { "finance-api", "calendar" });

        result.NoneAvailable.Should().BeTrue();
        result.CoverageRatio.Should().Be(0.0);
        result.AvailableTools.Should().BeEmpty();
        result.MissingTools.Should().HaveCount(2);
    }

    [Fact]
    public async Task CheckAsync_MissingTools_InvokesDiscoveryService()
    {
        _toolManager.GetTool("jira").Returns((ITool?)null);
        var suggestions = new List<ToolSuggestion>
        {
            new() { ToolName = "jira", PackageName = "@anthropic/mcp-server-atlassian", RelevanceScore = 0.9 }
        };
        _discoveryService.SearchAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(suggestions);

        var result = await _sut.CheckAsync(new[] { "jira" });

        result.Suggestions.Should().HaveCount(1);
        result.Suggestions[0].PackageName.Should().Be("@anthropic/mcp-server-atlassian");
    }

    [Fact]
    public async Task CheckAsync_ToolNotAvailable_TreatedAsMissing()
    {
        var tool = Substitute.For<ITool>();
        tool.Id.Returns("broken-tool");
        tool.IsAvailableAsync(Arg.Any<CancellationToken>()).Returns(false);
        _toolManager.GetTool("broken-tool").Returns(tool);

        var result = await _sut.CheckAsync(new[] { "broken-tool" });

        result.MissingTools.Should().Contain("broken-tool");
        result.AllAvailable.Should().BeFalse();
    }

    private void SetupTool(string id, bool available)
    {
        var tool = Substitute.For<ITool>();
        tool.Id.Returns(id);
        tool.IsAvailableAsync(Arg.Any<CancellationToken>()).Returns(available);
        _toolManager.GetTool(id).Returns(tool);
    }
}
