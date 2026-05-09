using System.Text;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using Microsoft.Extensions.Logging;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis.PageSegmenter;
using UglyToad.PdfPig.DocumentLayoutAnalysis.WordExtractor;

namespace AgenticSystem.Infrastructure.Documents;

/// <summary>
/// PDF parser using PdfPig — pure .NET library without native dependencies.
/// Extracts text page-by-page, preserves section structure, and detects embedded images.
/// </summary>
public class PdfDocumentParser : IDocumentParser
{
    private readonly ILogger<PdfDocumentParser> _logger;

    public PdfDocumentParser(ILogger<PdfDocumentParser> logger)
    {
        _logger = logger;
    }

    public DocumentType SupportedType => DocumentType.Pdf;

    public Task<ParsedDocument> ParseAsync(RawDocument document, CancellationToken ct = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var sections = new List<DocumentSection>();
        var fullText = new StringBuilder();
        var imageCount = 0;

        try
        {
            using var stream = new MemoryStream(document.Content);
            using var pdf = PdfDocument.Open(stream);

            for (int pageNum = 1; pageNum <= pdf.NumberOfPages; pageNum++)
            {
                ct.ThrowIfCancellationRequested();
                var page = pdf.GetPage(pageNum);

                // Extract text using word extraction
                var words = page.GetWords(NearestNeighbourWordExtractor.Instance);
                var blocks = DocstrumBoundingBoxes.Instance.GetBlocks(words);

                var pageText = new StringBuilder();
                foreach (var block in blocks)
                {
                    pageText.AppendLine(block.Text);
                }

                var text = pageText.ToString().Trim();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    sections.Add(new DocumentSection
                    {
                        Index = pageNum - 1,
                        Title = $"Page {pageNum}",
                        Content = text,
                        Level = 1,
                        Type = SectionType.Paragraph
                    });

                    if (fullText.Length > 0) fullText.AppendLine();
                    fullText.Append(text);
                }

                // Detect embedded images (for later multimodal processing)
                try
                {
                    var images = page.GetImages();
                    imageCount += images.Count();
                }
                catch
                {
                    // Some PDFs have malformed image entries
                }
            }

            sw.Stop();
            _logger.LogInformation(
                "Parsed PDF {File}: {Pages} pages, {Sections} sections, {Images} images in {Ms}ms",
                document.FileName, pdf.NumberOfPages, sections.Count, imageCount, sw.ElapsedMilliseconds);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to parse PDF: {File}", document.FileName);
            throw;
        }

        var hash = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(document.Content)).ToLowerInvariant();

        var metadata = new Dictionary<string, string>(document.Metadata)
        {
            ["image_count"] = imageCount.ToString(),
            ["page_count"] = sections.Count.ToString()
        };

        return Task.FromResult(new ParsedDocument
        {
            Id = document.Id,
            FileName = document.FileName,
            OriginalType = DocumentType.Pdf,
            FullText = fullText.ToString(),
            Sections = sections,
            Source = document.Source,
            Metadata = metadata,
            ContentHash = hash
        });
    }
}
