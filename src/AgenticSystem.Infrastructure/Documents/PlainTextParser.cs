using System.Security.Cryptography;
using System.Text;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Infrastructure.Documents;

/// <summary>
/// Parser de texto plano — divide em parágrafos.
/// </summary>
public class PlainTextParser : IDocumentParser
{
    private readonly ILogger<PlainTextParser> _logger;

    public PlainTextParser(ILogger<PlainTextParser> logger)
    {
        _logger = logger;
    }

    public DocumentType SupportedType => DocumentType.PlainText;

    public Task<ParsedDocument> ParseAsync(RawDocument document, CancellationToken ct = default)
    {
        var text = document.TextContent ?? Encoding.UTF8.GetString(document.Content);
        var sections = ExtractParagraphs(text);
        var hash = ComputeHash(text);

        var parsed = new ParsedDocument
        {
            Id = document.Id,
            FileName = document.FileName,
            OriginalType = DocumentType.PlainText,
            FullText = text,
            Sections = sections,
            Source = document.Source,
            Metadata = new Dictionary<string, string>(document.Metadata),
            ContentHash = hash,
            ParsedAt = DateTime.UtcNow
        };

        _logger.LogDebug("📄 Parsed PlainText {File}: {Sections} paragraphs",
            document.FileName, sections.Count);

        return Task.FromResult(parsed);
    }

    private static List<DocumentSection> ExtractParagraphs(string text)
    {
        var sections = new List<DocumentSection>();
        var paragraphs = text.Split(new[] { "\n\n", "\r\n\r\n" }, StringSplitOptions.RemoveEmptyEntries);
        var index = 0;

        foreach (var paragraph in paragraphs)
        {
            var trimmed = paragraph.Trim();
            if (string.IsNullOrWhiteSpace(trimmed)) continue;

            sections.Add(new DocumentSection
            {
                Index = index++,
                Title = string.Empty,
                Content = trimmed,
                Level = 0,
                Type = SectionType.Paragraph
            });
        }

        return sections;
    }

    private static string ComputeHash(string content)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
    }
}
