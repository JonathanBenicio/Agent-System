using System.Diagnostics;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Infrastructure.AI;

/// <summary>
/// Multimodal processor using LLM Vision capabilities (GPT-4o, Gemini 2.5 Pro, etc.)
/// via IChatClient from Microsoft.Extensions.AI.
/// Supports: OCR (image-to-text), image captioning, table extraction from images,
/// and audio transcription (delegated to provider's native capabilities).
/// </summary>
public class LlmMultimodalProcessor : IMultimodalProcessor
{
    private readonly IChatClient _chatClient;
    private readonly ILogger<LlmMultimodalProcessor> _logger;

    private static readonly IReadOnlyList<string> _supportedMimeTypes =
    [
        // Images
        "image/png", "image/jpeg", "image/gif", "image/webp", "image/bmp", "image/tiff",
        // PDF (for image-embedded pages sent as rendered images)
        "application/pdf",
        // Audio (when provider supports natively)
        "audio/mp3", "audio/wav", "audio/mpeg", "audio/ogg", "audio/webm"
    ];

    public LlmMultimodalProcessor(IChatClient chatClient, ILogger<LlmMultimodalProcessor> logger)
    {
        _chatClient = chatClient;
        _logger = logger;
    }

    public IReadOnlyList<string> SupportedMimeTypes => _supportedMimeTypes;

    public async Task<MultimodalDocument> ProcessAsync(
        Stream documentStream, string fileName, string mimeType, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var contents = new List<ExtractedContent>();
        var data = await ReadStreamAsync(documentStream, ct);

        if (mimeType.StartsWith("image/"))
        {
            // For images: extract OCR text + generate caption
            var ocrContent = await ExtractOcrFromBytesAsync(data, mimeType, null, ct);
            if (!string.IsNullOrWhiteSpace(ocrContent.Content))
                contents.Add(ocrContent);

            var caption = await CaptionImageFromBytesAsync(data, mimeType, ct);
            if (!string.IsNullOrWhiteSpace(caption.Content))
                contents.Add(caption);
        }
        else if (mimeType.StartsWith("audio/"))
        {
            var transcription = await TranscribeAudioFromBytesAsync(data, mimeType, null, ct);
            if (!string.IsNullOrWhiteSpace(transcription.Content))
                contents.Add(transcription);
        }

        sw.Stop();

        var contentType = mimeType switch
        {
            _ when mimeType.StartsWith("image/") => MultimodalContentType.Image,
            _ when mimeType.StartsWith("audio/") => MultimodalContentType.Audio,
            "application/pdf" => MultimodalContentType.PDF,
            _ => MultimodalContentType.Mixed
        };

        return new MultimodalDocument
        {
            FileName = fileName,
            MimeType = mimeType,
            FileSizeBytes = data.Length,
            ContentType = contentType,
            ExtractedContents = contents,
            ProcessingTime = sw.Elapsed
        };
    }

    public async Task<ExtractedContent> ExtractOcrAsync(
        Stream imageStream, string? language = null, CancellationToken ct = default)
    {
        var data = await ReadStreamAsync(imageStream, ct);
        return await ExtractOcrFromBytesAsync(data, "image/png", language, ct);
    }

    public async Task<IReadOnlyList<ExtractedContent>> ExtractTablesAsync(
        Stream documentStream, string mimeType, CancellationToken ct = default)
    {
        var data = await ReadStreamAsync(documentStream, ct);

        var prompt = """
            Analyze this document image and extract ALL tables present.
            For each table, output the content in a clean markdown table format.
            If no tables are found, respond with "NO_TABLES_FOUND".
            Preserve all cell values accurately. Include table headers.
            """;

        var response = await SendVisionRequestAsync(data, mimeType, prompt, ct);
        var text = response?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(text) || text.Contains("NO_TABLES_FOUND"))
            return [];

        // Split by double newlines to separate multiple tables
        var tables = text.Split("\n\n", StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Contains('|')) // Only keep markdown table segments
            .Select((t, idx) => new ExtractedContent
            {
                ExtractionType = ExtractionType.TableExtraction,
                Content = t.Trim(),
                PageNumber = 1,
                Confidence = 0.85
            })
            .ToList();

        _logger.LogInformation("Extracted {Count} tables from document", tables.Count);
        return tables;
    }

    public async Task<ExtractedContent> CaptionImageAsync(
        Stream imageStream, CancellationToken ct = default)
    {
        var data = await ReadStreamAsync(imageStream, ct);
        return await CaptionImageFromBytesAsync(data, "image/png", ct);
    }

    public async Task<ExtractedContent> TranscribeAudioAsync(
        Stream audioStream, string? language = null, CancellationToken ct = default)
    {
        var data = await ReadStreamAsync(audioStream, ct);
        return await TranscribeAudioFromBytesAsync(data, "audio/mpeg", language, ct);
    }

    // ═══════════════════════════════════════════════════════════
    // Internal Methods
    // ═══════════════════════════════════════════════════════════

    private async Task<ExtractedContent> ExtractOcrFromBytesAsync(
        byte[] data, string mimeType, string? language, CancellationToken ct)
    {
        var langHint = language != null ? $" The text is in {language}." : "";
        var prompt = $"""
            Extract ALL text visible in this image using OCR.
            Preserve the original layout, formatting, line breaks, and structure as much as possible.
            Include all headers, paragraphs, lists, and any visible text.{langHint}
            Output ONLY the extracted text, nothing else.
            """;

        var text = await SendVisionRequestAsync(data, mimeType, prompt, ct);

        return new ExtractedContent
        {
            ExtractionType = ExtractionType.OCR,
            Content = text?.Trim() ?? string.Empty,
            Confidence = string.IsNullOrWhiteSpace(text) ? 0 : 0.9,
            Language = language
        };
    }

    private async Task<ExtractedContent> CaptionImageFromBytesAsync(
        byte[] data, string mimeType, CancellationToken ct)
    {
        var prompt = """
            Describe this image in detail. Include:
            - What is depicted (objects, people, scenes, diagrams)
            - Any text or labels visible
            - Colors, layout, and composition
            - Context and meaning if it appears to be a chart, diagram, or technical illustration
            Provide a concise but comprehensive description suitable for search indexing.
            """;

        var text = await SendVisionRequestAsync(data, mimeType, prompt, ct);

        return new ExtractedContent
        {
            ExtractionType = ExtractionType.ImageCaption,
            Content = text?.Trim() ?? string.Empty,
            Confidence = string.IsNullOrWhiteSpace(text) ? 0 : 0.85
        };
    }

    private async Task<ExtractedContent> TranscribeAudioFromBytesAsync(
        byte[] data, string mimeType, string? language, CancellationToken ct)
    {
        // Audio transcription via multimodal LLM (Gemini supports native audio)
        // For providers that don't support audio, this will gracefully fail
        try
        {
            var langHint = language != null ? $" The audio is in {language}." : "";
            var prompt = $"Transcribe the following audio accurately.{langHint} Output ONLY the transcription text.";

            var messages = new List<ChatMessage>
            {
                new(ChatRole.User,
                [
                    new DataContent(data, mimeType),
                    new TextContent(prompt)
                ])
            };

            var response = await _chatClient.GetResponseAsync(messages, cancellationToken: ct);
            var text = response.Text?.Trim() ?? string.Empty;

            return new ExtractedContent
            {
                ExtractionType = ExtractionType.AudioTranscription,
                Content = text,
                Confidence = string.IsNullOrWhiteSpace(text) ? 0 : 0.8,
                Language = language
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Audio transcription failed — provider may not support audio input");
            return new ExtractedContent
            {
                ExtractionType = ExtractionType.AudioTranscription,
                Content = string.Empty,
                Confidence = 0
            };
        }
    }

    private async Task<string?> SendVisionRequestAsync(
        byte[] imageData, string mimeType, string prompt, CancellationToken ct)
    {
        try
        {
            var messages = new List<ChatMessage>
            {
                new(ChatRole.User,
                [
                    new DataContent(imageData, mimeType),
                    new TextContent(prompt)
                ])
            };

            var response = await _chatClient.GetResponseAsync(messages, cancellationToken: ct);
            return response.Text;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Vision request failed for prompt: {Prompt}", prompt[..Math.Min(50, prompt.Length)]);
            return null;
        }
    }

    private static async Task<byte[]> ReadStreamAsync(Stream stream, CancellationToken ct)
    {
        if (stream is MemoryStream ms)
            return ms.ToArray();

        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, ct);
        return buffer.ToArray();
    }
}
