using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.Chunking;
using AgenticSystem.Infrastructure.Documents;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace AgenticSystem.Tests;

public class HybridChunkingStrategyTests
{
    private readonly HybridChunkingStrategy _strategy;
    private readonly MarkdownParser _parser;

    public HybridChunkingStrategyTests()
    {
        _strategy = new HybridChunkingStrategy(Substitute.For<ILogger<HybridChunkingStrategy>>());
        _parser = new MarkdownParser(Substitute.For<ILogger<MarkdownParser>>());
    }

    [Fact]
    public void StrategyType_ShouldBeHybrid()
    {
        _strategy.StrategyType.Should().Be(ChunkingStrategyType.Hybrid);
    }

    [Fact]
    public async Task ChunkAsync_SmallDocument_ShouldCreateSingleChunk()
    {
        var parsed = await ParseMarkdown("# Title\nShort content.");
        var config = new ChunkingConfig { TargetTokens = 500, MinTokens = 5 };

        var chunks = await _strategy.ChunkAsync(parsed, config);

        chunks.Should().HaveCount(1);
        chunks[0].Content.Should().Contain("Short content");
        chunks[0].Metadata.FileName.Should().Be("test.md");
    }

    [Fact]
    public async Task ChunkAsync_LargeDocument_ShouldCreateMultipleChunks()
    {
        // Generate content well over TargetTokens (each line ~25 chars = ~6 tokens)
        var sections = string.Join("\n\n", Enumerable.Range(1, 100)
            .Select(i => $"## Section {i}\nThis is paragraph number {i} with enough content to fill tokens."));
        var parsed = await ParseMarkdown(sections);
        var config = new ChunkingConfig { TargetTokens = 100, MaxTokens = 200, MinTokens = 10 };

        var chunks = await _strategy.ChunkAsync(parsed, config);

        chunks.Should().HaveCountGreaterThan(1);
        chunks.Select(c => c.Index).Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task ChunkAsync_ShouldAssignGlobalIndices()
    {
        var md = "# A\nContent A.\n\n# B\nContent B.\n\n# C\nContent C.";
        var parsed = await ParseMarkdown(md);
        var config = new ChunkingConfig { TargetTokens = 20, MinTokens = 5 };

        var chunks = await _strategy.ChunkAsync(parsed, config);

        for (var i = 0; i < chunks.Count; i++)
        {
            chunks[i].Index.Should().Be(i);
            chunks[i].Metadata.ChunkIndex.Should().Be(i);
            chunks[i].Metadata.TotalChunks.Should().Be(chunks.Count);
        }
    }

    [Fact]
    public async Task ChunkAsync_NoSections_FallsBackToFixedSize()
    {
        var parsed = new ParsedDocument
        {
            Id = "test",
            FileName = "plain.txt",
            OriginalType = DocumentType.PlainText,
            FullText = string.Join(". ", Enumerable.Range(1, 200).Select(i => $"Sentence number {i}")),
            Sections = new List<DocumentSection>(),
            ContentHash = "abc123"
        };
        var config = new ChunkingConfig { TargetTokens = 100, MinTokens = 10 };

        var chunks = await _strategy.ChunkAsync(parsed, config);

        chunks.Should().HaveCountGreaterThan(1);
    }

    [Fact]
    public async Task ChunkAsync_ShouldPopulateMetadata()
    {
        var parsed = await ParseMarkdown("# Test\nSome content here.");
        var config = new ChunkingConfig
        {
            TargetTokens = 500,
            MinTokens = 5,
            Collection = "docs",
            ContentType = "test-doc"
        };

        var chunks = await _strategy.ChunkAsync(parsed, config);

        chunks.Should().HaveCountGreaterThanOrEqualTo(1);
        var meta = chunks[0].Metadata;
        meta.Collection.Should().Be("docs");
        meta.ContentType.Should().Be("test-doc");
        meta.FileName.Should().Be("test.md");
    }

    [Fact]
    public void EstimateTokens_ShouldBeReasonable()
    {
        HybridChunkingStrategy.EstimateTokens("Hello World").Should().BeGreaterThan(0);
        HybridChunkingStrategy.EstimateTokens("").Should().Be(0);
        HybridChunkingStrategy.EstimateTokens(null!).Should().Be(0);
    }

    private async Task<ParsedDocument> ParseMarkdown(string md)
    {
        var raw = new RawDocument
        {
            FileName = "test.md",
            Type = DocumentType.Markdown,
            TextContent = md,
            Content = System.Text.Encoding.UTF8.GetBytes(md),
            Source = "test"
        };
        return await _parser.ParseAsync(raw);
    }
}
