using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.Documents;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace AgenticSystem.Tests;

public class MarkdownParserTests
{
    private readonly MarkdownParser _parser;

    public MarkdownParserTests()
    {
        var logger = Substitute.For<ILogger<MarkdownParser>>();
        _parser = new MarkdownParser(logger);
    }

    [Fact]
    public void SupportedType_ShouldBeMarkdown()
    {
        _parser.SupportedType.Should().Be(DocumentType.Markdown);
    }

    [Fact]
    public async Task ParseAsync_SimpleHeaders_ShouldExtractSections()
    {
        var doc = CreateRawDocument("# Title\nParagraph one.\n\n## Subtitle\nParagraph two.", "test.md");

        var result = await _parser.ParseAsync(doc);

        result.FileName.Should().Be("test.md");
        result.OriginalType.Should().Be(DocumentType.Markdown);
        result.Sections.Should().HaveCountGreaterThanOrEqualTo(2);
        result.Sections.Should().Contain(s => s.Title == "Title");
        result.Sections.Should().Contain(s => s.Title == "Subtitle");
        result.ContentHash.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ParseAsync_Frontmatter_ShouldExtractAsSeparateSection()
    {
        var md = "---\ntitle: Test\ndate: 2024-01-01\n---\n\n# Content\nBody text.";
        var doc = CreateRawDocument(md, "frontmatter.md");

        var result = await _parser.ParseAsync(doc);

        result.Sections.Should().Contain(s => s.Type == SectionType.Frontmatter);
    }

    [Fact]
    public async Task ParseAsync_CodeBlocks_ShouldPreserveAsCodeBlockType()
    {
        var md = "# Code\n```csharp\nvar x = 1;\n```\n\n# After";
        var doc = CreateRawDocument(md, "code.md");

        var result = await _parser.ParseAsync(doc);

        result.Sections.Should().Contain(s => s.Type == SectionType.CodeBlock);
    }

    [Fact]
    public async Task ParseAsync_SameContent_ShouldProduceSameHash()
    {
        var content = "# Deterministic\nSame content.";
        var doc1 = CreateRawDocument(content, "a.md");
        var doc2 = CreateRawDocument(content, "b.md");

        var result1 = await _parser.ParseAsync(doc1);
        var result2 = await _parser.ParseAsync(doc2);

        result1.ContentHash.Should().Be(result2.ContentHash);
    }

    [Fact]
    public async Task ParseAsync_EmptyContent_ShouldReturnEmptySections()
    {
        var doc = CreateRawDocument("", "empty.md");

        var result = await _parser.ParseAsync(doc);

        result.Sections.Should().BeEmpty();
        result.FullText.Should().BeEmpty();
    }

    private static RawDocument CreateRawDocument(string text, string fileName)
    {
        return new RawDocument
        {
            FileName = fileName,
            Type = DocumentType.Markdown,
            TextContent = text,
            Content = System.Text.Encoding.UTF8.GetBytes(text),
            Source = "test"
        };
    }
}
