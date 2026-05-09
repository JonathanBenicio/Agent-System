using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Interfaces;

/// <summary>
/// Service for processing multimodal documents and extracting content
/// for RAG pipeline ingestion. Supports OCR, table extraction, layout analysis,
/// audio transcription, and image captioning.
/// </summary>
public interface IMultimodalProcessor
{
    /// <summary>
    /// Processes a multimodal document and extracts all content types.
    /// </summary>
    Task<MultimodalDocument> ProcessAsync(
        Stream documentStream,
        string fileName,
        string mimeType,
        CancellationToken ct = default);

    /// <summary>
    /// Extracts text from an image using OCR.
    /// </summary>
    Task<ExtractedContent> ExtractOcrAsync(
        Stream imageStream,
        string? language = null,
        CancellationToken ct = default);

    /// <summary>
    /// Extracts tables from a document page.
    /// </summary>
    Task<IReadOnlyList<ExtractedContent>> ExtractTablesAsync(
        Stream documentStream,
        string mimeType,
        CancellationToken ct = default);

    /// <summary>
    /// Generates a text caption/description for an image.
    /// </summary>
    Task<ExtractedContent> CaptionImageAsync(
        Stream imageStream,
        CancellationToken ct = default);

    /// <summary>
    /// Transcribes audio content to text.
    /// </summary>
    Task<ExtractedContent> TranscribeAudioAsync(
        Stream audioStream,
        string? language = null,
        CancellationToken ct = default);

    /// <summary>
    /// Returns the supported MIME types for processing.
    /// </summary>
    IReadOnlyList<string> SupportedMimeTypes { get; }
}
