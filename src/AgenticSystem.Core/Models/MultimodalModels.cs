namespace AgenticSystem.Core.Models;

// ═══════════════════════════════════════════════════════════
// Multimodal RAG — Content Types & Extraction
// ═══════════════════════════════════════════════════════════

/// <summary>
/// Represents a multimodal document with extracted content from different modalities.
/// </summary>
public class MultimodalDocument
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string FileName { get; init; } = string.Empty;
    public string MimeType { get; init; } = string.Empty;
    public long FileSizeBytes { get; init; }
    public MultimodalContentType ContentType { get; init; }
    public List<ExtractedContent> ExtractedContents { get; init; } = [];
    public Dictionary<string, string> Metadata { get; init; } = new();
    public DateTime ProcessedAt { get; init; } = DateTime.UtcNow;
    public TimeSpan ProcessingTime { get; init; }
}

/// <summary>
/// A single piece of content extracted from a multimodal document.
/// </summary>
public class ExtractedContent
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public ExtractionType ExtractionType { get; init; }
    public string Content { get; init; } = string.Empty;
    public int? PageNumber { get; init; }
    public BoundingBox? Region { get; init; }
    public double Confidence { get; init; } = 1.0;
    public string? Language { get; init; }
}

/// <summary>
/// Bounding box for spatial location in a page/image.
/// </summary>
public class BoundingBox
{
    public double X { get; init; }
    public double Y { get; init; }
    public double Width { get; init; }
    public double Height { get; init; }
}

public enum MultimodalContentType
{
    Text,
    PDF,
    Image,
    Audio,
    Video,
    Spreadsheet,
    Presentation,
    Mixed
}

public enum ExtractionType
{
    FullText,
    OCR,
    TableExtraction,
    ImageCaption,
    LayoutAnalysis,
    AudioTranscription,
    CodeBlock,
    Metadata
}
