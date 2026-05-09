using System.Text;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Infrastructure.Documents;

/// <summary>
/// DOCX parser using OpenXml SDK — extracts text content, headings, tables, and lists.
/// Also tracks embedded images for multimodal processing.
/// </summary>
public class DocxDocumentParser : IDocumentParser
{
    private readonly ILogger<DocxDocumentParser> _logger;

    public DocxDocumentParser(ILogger<DocxDocumentParser> logger)
    {
        _logger = logger;
    }

    public AgenticSystem.Core.Models.DocumentType SupportedType => AgenticSystem.Core.Models.DocumentType.Docx;

    public Task<ParsedDocument> ParseAsync(RawDocument document, CancellationToken ct = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var sections = new List<DocumentSection>();
        var fullText = new StringBuilder();
        var imageCount = 0;

        try
        {
            using var stream = new MemoryStream(document.Content);
            using var wordDoc = WordprocessingDocument.Open(stream, false);
            var body = wordDoc.MainDocumentPart?.Document.Body;

            if (body == null)
            {
                return Task.FromResult(CreateEmptyResult(document));
            }

            var currentSection = new StringBuilder();
            var currentTitle = "Document";
            var currentLevel = 0;
            var sectionIndex = 0;

            foreach (var element in body.Elements())
            {
                ct.ThrowIfCancellationRequested();

                if (element is Paragraph para)
                {
                    var text = para.InnerText?.Trim();
                    if (string.IsNullOrWhiteSpace(text)) continue;

                    var style = para.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
                    var headingLevel = GetHeadingLevel(style);

                    if (headingLevel > 0)
                    {
                        // Flush current section
                        if (currentSection.Length > 0)
                        {
                            sections.Add(new DocumentSection
                            {
                                Index = sectionIndex++,
                                Title = currentTitle,
                                Content = currentSection.ToString().Trim(),
                                Level = currentLevel,
                                Type = currentLevel > 0 ? AgenticSystem.Core.Models.SectionType.Heading : AgenticSystem.Core.Models.SectionType.Paragraph
                            });
                            currentSection.Clear();
                        }

                        currentTitle = text;
                        currentLevel = headingLevel;
                    }
                    else
                    {
                        currentSection.AppendLine(text);
                    }

                    fullText.AppendLine(text);

                    // Check for inline images
                    var drawings = para.Descendants<DocumentFormat.OpenXml.Wordprocessing.Drawing>();
                    imageCount += drawings.Count();
                }
                else if (element is Table table)
                {
                    var tableText = ExtractTableText(table);
                    if (!string.IsNullOrWhiteSpace(tableText))
                    {
                        sections.Add(new DocumentSection
                        {
                            Index = sectionIndex++,
                            Title = "Table",
                            Content = tableText,
                            Level = 0,
                            Type = AgenticSystem.Core.Models.SectionType.Table
                        });
                        fullText.AppendLine(tableText);
                    }
                }
            }

            // Flush last section
            if (currentSection.Length > 0)
            {
                sections.Add(new DocumentSection
                {
                    Index = sectionIndex,
                    Title = currentTitle,
                    Content = currentSection.ToString().Trim(),
                    Level = currentLevel,
                    Type = currentLevel > 0 ? AgenticSystem.Core.Models.SectionType.Heading : AgenticSystem.Core.Models.SectionType.Paragraph
                });
            }

            // Count images in the document package
            imageCount += wordDoc.MainDocumentPart?.ImageParts.Count() ?? 0;

            sw.Stop();
            _logger.LogInformation(
                "Parsed DOCX {File}: {Sections} sections, {Images} images in {Ms}ms",
                document.FileName, sections.Count, imageCount, sw.ElapsedMilliseconds);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to parse DOCX: {File}", document.FileName);
            throw;
        }

        var hash = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(document.Content)).ToLowerInvariant();

        var metadata = new Dictionary<string, string>(document.Metadata)
        {
            ["image_count"] = imageCount.ToString(),
            ["section_count"] = sections.Count.ToString()
        };

        return Task.FromResult(new ParsedDocument
        {
            Id = document.Id,
            FileName = document.FileName,
            OriginalType = AgenticSystem.Core.Models.DocumentType.Docx,
            FullText = fullText.ToString(),
            Sections = sections,
            Source = document.Source,
            Metadata = metadata,
            ContentHash = hash
        });
    }

    private static int GetHeadingLevel(string? styleId)
    {
        if (string.IsNullOrWhiteSpace(styleId)) return 0;

        return styleId.ToLowerInvariant() switch
        {
            "heading1" or "titre1" or "berschrift1" => 1,
            "heading2" or "titre2" or "berschrift2" => 2,
            "heading3" or "titre3" or "berschrift3" => 3,
            "heading4" or "titre4" or "berschrift4" => 4,
            "heading5" or "titre5" or "berschrift5" => 5,
            "heading6" or "titre6" or "berschrift6" => 6,
            "title" or "titulo" => 1,
            "subtitle" or "subtitulo" => 2,
            _ => 0
        };
    }

    private static string ExtractTableText(Table table)
    {
        var sb = new StringBuilder();
        var rows = table.Elements<TableRow>().ToList();

        foreach (var row in rows)
        {
            var cells = row.Elements<TableCell>().Select(c => c.InnerText?.Trim() ?? "");
            sb.AppendLine("| " + string.Join(" | ", cells) + " |");
        }

        return sb.ToString();
    }

    private static ParsedDocument CreateEmptyResult(RawDocument document) => new()
    {
        Id = document.Id,
        FileName = document.FileName,
        OriginalType = AgenticSystem.Core.Models.DocumentType.Docx,
        FullText = string.Empty,
        Sections = [],
        Source = document.Source,
        Metadata = document.Metadata,
        ContentHash = string.Empty
    };
}
