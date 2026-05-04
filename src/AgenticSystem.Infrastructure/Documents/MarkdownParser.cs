using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Infrastructure.Documents;

/// <summary>
/// Parser de Markdown — extrai seções por headers, code blocks, listas e frontmatter.
/// </summary>
public class MarkdownParser : IDocumentParser
{
    private readonly ILogger<MarkdownParser> _logger;

    public MarkdownParser(ILogger<MarkdownParser> logger)
    {
        _logger = logger;
    }

    public DocumentType SupportedType => DocumentType.Markdown;

    public Task<ParsedDocument> ParseAsync(RawDocument document, CancellationToken ct = default)
    {
        var text = document.TextContent ?? Encoding.UTF8.GetString(document.Content);
        var sections = ExtractSections(text);
        var hash = ComputeHash(text);

        var parsed = new ParsedDocument
        {
            Id = document.Id,
            FileName = document.FileName,
            OriginalType = DocumentType.Markdown,
            FullText = text,
            Sections = sections,
            Source = document.Source,
            Metadata = new Dictionary<string, string>(document.Metadata),
            ContentHash = hash,
            ParsedAt = DateTime.UtcNow
        };

        _logger.LogDebug("📄 Parsed Markdown {File}: {Sections} sections, hash={Hash}",
            document.FileName, sections.Count, hash[..8]);

        return Task.FromResult(parsed);
    }

    private static List<DocumentSection> ExtractSections(string text)
    {
        var sections = new List<DocumentSection>();
        var lines = text.Split('\n');
        var currentSection = new DocumentSection { Index = 0, Type = SectionType.Paragraph };
        var contentBuilder = new StringBuilder();
        var sectionIndex = 0;
        var inCodeBlock = false;
        var inFrontmatter = false;

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];

            // Frontmatter detection (---...---)
            if (i == 0 && line.TrimEnd() == "---")
            {
                inFrontmatter = true;
                contentBuilder.AppendLine(line);
                continue;
            }

            if (inFrontmatter && line.TrimEnd() == "---")
            {
                inFrontmatter = false;
                contentBuilder.AppendLine(line);
                sections.Add(new DocumentSection
                {
                    Index = sectionIndex++,
                    Title = "Frontmatter",
                    Content = contentBuilder.ToString().Trim(),
                    Level = 0,
                    Type = SectionType.Frontmatter
                });
                contentBuilder.Clear();
                continue;
            }

            if (inFrontmatter)
            {
                contentBuilder.AppendLine(line);
                continue;
            }

            // Code block toggle
            if (line.TrimStart().StartsWith("```"))
            {
                inCodeBlock = !inCodeBlock;
                if (inCodeBlock)
                {
                    FlushSection(sections, currentSection, contentBuilder, ref sectionIndex);
                    currentSection = new DocumentSection { Type = SectionType.CodeBlock };
                }
                contentBuilder.AppendLine(line);
                if (!inCodeBlock)
                {
                    FlushSection(sections, currentSection, contentBuilder, ref sectionIndex);
                    currentSection = new DocumentSection { Type = SectionType.Paragraph };
                }
                continue;
            }

            if (inCodeBlock)
            {
                contentBuilder.AppendLine(line);
                continue;
            }

            // Header detection
            var headerMatch = Regex.Match(line, @"^(#{1,6})\s+(.+)$");
            if (headerMatch.Success)
            {
                FlushSection(sections, currentSection, contentBuilder, ref sectionIndex);

                var level = headerMatch.Groups[1].Value.Length;
                var title = headerMatch.Groups[2].Value.Trim();
                currentSection = new DocumentSection
                {
                    Title = title,
                    Level = level,
                    Type = SectionType.Heading
                };
                continue;
            }

            contentBuilder.AppendLine(line);
        }

        FlushSection(sections, currentSection, contentBuilder, ref sectionIndex);

        return sections;
    }

    private static void FlushSection(List<DocumentSection> sections, DocumentSection current,
        StringBuilder contentBuilder, ref int sectionIndex)
    {
        var content = contentBuilder.ToString().Trim();
        if (string.IsNullOrWhiteSpace(content) && string.IsNullOrEmpty(current.Title))
            return;

        current.Index = sectionIndex++;
        current.Content = content;
        sections.Add(current);
        contentBuilder.Clear();
    }

    private static string ComputeHash(string content)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
    }
}
