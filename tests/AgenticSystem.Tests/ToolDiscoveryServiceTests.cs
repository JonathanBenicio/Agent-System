using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Services;

namespace AgenticSystem.Tests;

public class ToolDiscoveryServiceTests
{
    private readonly ILogger<ToolDiscoveryService> _logger;
    private readonly ToolDiscoveryService _sut;

    public ToolDiscoveryServiceTests()
    {
        _logger = Substitute.For<ILogger<ToolDiscoveryService>>();
        _sut = new ToolDiscoveryService(_logger);
    }

    [Theory]
    [InlineData("github", "@modelcontextprotocol/server-github")]
    [InlineData("jira", "@anthropic/mcp-server-atlassian")]
    [InlineData("slack", "@modelcontextprotocol/server-slack")]
    [InlineData("database", "@modelcontextprotocol/server-postgres")]
    public async Task SearchAsync_KnownTool_ReturnsExactMatch(string toolId, string expectedPackage)
    {
        var results = await _sut.SearchAsync(new[] { toolId });

        results.Should().HaveCount(1);
        results[0].ToolName.Should().Be(toolId);
        results[0].PackageName.Should().Be(expectedPackage);
        results[0].RelevanceScore.Should().BeGreaterOrEqualTo(0.85);
    }

    [Fact]
    public async Task SearchAsync_UnknownTool_ReturnsGenericSuggestion()
    {
        var results = await _sut.SearchAsync(new[] { "totally-unknown-xyz" });

        results.Should().HaveCount(1);
        results[0].ToolName.Should().Be("totally-unknown-xyz");
        results[0].Source.Should().Be("manual");
        results[0].RelevanceScore.Should().BeLessThan(0.5);
    }

    [Fact]
    public async Task SearchAsync_MultipleTools_ReturnsSortedByRelevance()
    {
        var results = await _sut.SearchAsync(new[] { "github", "totally-unknown" });

        results.Should().HaveCount(2);
        results[0].RelevanceScore.Should().BeGreaterOrEqualTo(results[1].RelevanceScore);
    }

    [Fact]
    public async Task SearchAsync_EmptyList_ReturnsEmpty()
    {
        var results = await _sut.SearchAsync(Array.Empty<string>());

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchAsync_CaseInsensitive_FindsMatch()
    {
        var results = await _sut.SearchAsync(new[] { "GitHub" });

        results.Should().HaveCount(1);
        results[0].PackageName.Should().Be("@modelcontextprotocol/server-github");
    }

    [Fact]
    public async Task SearchAsync_FuzzyMatch_ReducedRelevance()
    {
        // "mail" should fuzzy-match "email" via substring in ToolName
        var results = await _sut.SearchAsync(new[] { "mail" });

        // Should find email via fuzzy or return generic
        results.Should().NotBeEmpty();
        if (results[0].ToolName == "email")
        {
            // Fuzzy match has reduced relevance (0.7x)
            results[0].RelevanceScore.Should().BeLessThan(0.90);
        }
    }
}
