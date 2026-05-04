using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Infrastructure.Documents;

/// <summary>
/// Parser de HTML — extrai texto estruturado removendo tags.
/// </summary>
public class HtmlParser : IDocumentParser
{
    private readonly ILogger<HtmlParser> _logger;

    public HtmlParser(ILogger<HtmlParser> logger)
    {
        _logger = logger;
    }

    public DocumentType SupportedType => DocumentType.Html;

    public Task<ParsedDocument> ParseAsync(RawDocument document, CancellationToken ct = default)
    {
        var html = document.TextContent ?? Encoding.UTF8.GetString(document.Content);
        var sections = ExtractSections(html);
        var fullText = string.Join("\n\n", sections.Select(s => s.Content));
        var hash = ComputeHash(html);

        var parsed = new ParsedDocument
        {
            Id = document.Id,
            FileName = document.FileName,
            OriginalType = DocumentType.Html,
            FullText = fullText,
            Sections = sections,
            Source = document.Source,
            Metadata = new Dictionary<string, string>(document.Metadata),
            ContentHash = hash,
            ParsedAt = DateTime.UtcNow
        };

        _logger.LogDebug("📄 Parsed HTML {File}: {Sections} sections", document.FileName, sections.Count);
        return Task.FromResult(parsed);
    }

    private static List<DocumentSection> ExtractSections(string html)
    {
        var sections = new List<DocumentSection>();
        var index = 0;

        // Extract headings and their following content
        var headerPattern = new Regex(@"<h([1-6])[^>]*>(.*?)</h\1>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        var matches = headerPattern.Matches(html);

        if (matches.Count == 0)
        {
            // No headers — treat entire body as one section
            var bodyText = StripTags(html);
            if (!string.IsNullOrWhiteSpace(bodyText))
            {
                sections.Add(new DocumentSection
                {
                    Index = 0,
                    Title = string.Empty,
                    Content = bodyText.Trim(),
                    Level = 0,
                    Type = SectionType.Paragraph
                });
            }
            return sections;
        }

        for (var i = 0; i < matches.Count; i++)
        {
            var match = matches[i];
            var level = int.Parse(match.Groups[1].Value);
            var title = StripTags(match.Groups[2].Value);

            var contentStart = match.Index + match.Length;
            var contentEnd = i + 1 < matches.Count ? matches[i + 1].Index : html.Length;
            var rawContent = html[contentStart..contentEnd];
            var content = StripTags(rawContent).Trim();

            sections.Add(new DocumentSection
            {
                Index = index++,
                Title = title,
                Content = content,
                Level = level,
                Type = SectionType.Heading
            });
        }

        return sections;
    }

    private static string StripTags(string html)
    {
        var noTags = Regex.Replace(html, @"<[^>]+>", " ");
        var decoded = System.Net.WebUtility.HtmlDecode(noTags);
        return Regex.Replace(decoded, @"\s+", " ").Trim();
    }

    private static string ComputeHash(string content)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
    }
}
