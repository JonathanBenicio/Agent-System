using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Infrastructure.Chunking;

/// <summary>
/// Estratégia híbrida de chunking: respeita fronteiras estruturais (sections)
/// e subdivide por tamanho quando necessário, com overlap configurável.
/// 
/// Fluxo:
/// 1. Agrupa seções contíguas respeitando MaxTokens
/// 2. Se uma seção ultrapassa MaxTokens, divide por sentenças com overlap
/// 3. Seções menores que MinTokens são mescladas com a próxima
/// </summary>
public class HybridChunkingStrategy : IChunkingStrategy
{
    private readonly ILogger<HybridChunkingStrategy> _logger;

    public HybridChunkingStrategy(ILogger<HybridChunkingStrategy> logger)
    {
        _logger = logger;
    }

    public ChunkingStrategyType StrategyType => ChunkingStrategyType.Hybrid;

    public Task<IReadOnlyList<DocumentChunk>> ChunkAsync(
        ParsedDocument document, ChunkingConfig config, CancellationToken ct = default)
    {
        var chunks = new List<DocumentChunk>();
        var sections = document.Sections;

        if (sections.Count == 0 && !string.IsNullOrWhiteSpace(document.FullText))
        {
            // No sections — fallback to fixed-size chunking on full text
            chunks.AddRange(ChunkBySize(document, document.FullText, string.Empty, 0, config));
        }
        else
        {
            chunks.AddRange(ChunkSections(document, sections, config));
        }

        // Assign global indices and total
        var totalChunks = chunks.Count;
        for (var i = 0; i < chunks.Count; i++)
        {
            chunks[i].Index = i;
            chunks[i].Metadata.ChunkIndex = i;
            chunks[i].Metadata.TotalChunks = totalChunks;
        }

        _logger.LogDebug("✂️ Chunked {File}: {Count} chunks (strategy=Hybrid, target={Target}t, max={Max}t, overlap={Overlap}%)",
            document.FileName, chunks.Count, config.TargetTokens, config.MaxTokens, config.OverlapPercent * 100);

        return Task.FromResult<IReadOnlyList<DocumentChunk>>(chunks);
    }

    private List<DocumentChunk> ChunkSections(ParsedDocument document, List<DocumentSection> sections, ChunkingConfig config)
    {
        var chunks = new List<DocumentChunk>();
        var buffer = new List<(string title, string content, int level)>();
        var bufferTokens = 0;

        foreach (var section in sections)
        {
            var sectionText = BuildSectionText(section);
            var sectionTokens = EstimateTokens(sectionText);

            // Section larger than MaxTokens — flush buffer and split this section
            if (sectionTokens > config.MaxTokens)
            {
                FlushBuffer(chunks, document, buffer, ref bufferTokens, config);
                chunks.AddRange(ChunkBySize(document, sectionText, section.Title, section.Level, config));
                continue;
            }

            // Adding this section would exceed target — flush buffer
            if (bufferTokens + sectionTokens > config.TargetTokens && buffer.Count > 0)
            {
                FlushBuffer(chunks, document, buffer, ref bufferTokens, config);
            }

            buffer.Add((section.Title, sectionText, section.Level));
            bufferTokens += sectionTokens;
        }

        // Flush remaining
        FlushBuffer(chunks, document, buffer, ref bufferTokens, config);

        return chunks;
    }

    private void FlushBuffer(List<DocumentChunk> chunks, ParsedDocument document,
        List<(string title, string content, int level)> buffer, ref int bufferTokens, ChunkingConfig config)
    {
        if (buffer.Count == 0) return;

        var content = string.Join("\n\n", buffer.Select(b => b.content));
        var tokens = EstimateTokens(content);

        if (tokens < config.MinTokens && chunks.Count > 0)
        {
            // Too small — merge with previous chunk
            var lastChunk = chunks[^1];
            lastChunk.Content += "\n\n" + content;
            lastChunk.TokenCount = EstimateTokens(lastChunk.Content);
        }
        else
        {
            var primaryTitle = buffer.FirstOrDefault(b => !string.IsNullOrEmpty(b.title)).title ?? string.Empty;
            var level = buffer.FirstOrDefault(b => b.level > 0).level;

            chunks.Add(CreateChunk(document, content, primaryTitle, level, false, config));
        }

        buffer.Clear();
        bufferTokens = 0;
    }

    private List<DocumentChunk> ChunkBySize(ParsedDocument document, string text,
        string sectionTitle, int sectionLevel, ChunkingConfig config)
    {
        var chunks = new List<DocumentChunk>();
        var sentences = SplitIntoSentences(text);
        var overlapTokens = (int)(config.TargetTokens * config.OverlapPercent);

        var currentContent = new List<string>();
        var currentTokens = 0;

        foreach (var sentence in sentences)
        {
            var sentenceTokens = EstimateTokens(sentence);

            if (currentTokens + sentenceTokens > config.TargetTokens && currentContent.Count > 0)
            {
                var chunkText = string.Join(" ", currentContent);
                chunks.Add(CreateChunk(document, chunkText, sectionTitle, sectionLevel,
                    chunks.Count > 0, config));

                // Overlap: keep last N tokens worth of sentences
                var overlapContent = new List<string>();
                var overlapCount = 0;
                for (var i = currentContent.Count - 1; i >= 0 && overlapCount < overlapTokens; i--)
                {
                    overlapContent.Insert(0, currentContent[i]);
                    overlapCount += EstimateTokens(currentContent[i]);
                }

                currentContent = overlapContent;
                currentTokens = overlapCount;
            }

            currentContent.Add(sentence);
            currentTokens += sentenceTokens;
        }

        // Flush remaining
        if (currentContent.Count > 0)
        {
            var chunkText = string.Join(" ", currentContent);
            var tokens = EstimateTokens(chunkText);
            if (tokens >= config.MinTokens || chunks.Count == 0)
            {
                chunks.Add(CreateChunk(document, chunkText, sectionTitle, sectionLevel,
                    chunks.Count > 0, config));
            }
            else if (chunks.Count > 0)
            {
                // Merge small tail into last chunk
                chunks[^1].Content += " " + chunkText;
                chunks[^1].TokenCount = EstimateTokens(chunks[^1].Content);
            }
        }

        return chunks;
    }

    private static DocumentChunk CreateChunk(ParsedDocument document, string content,
        string sectionTitle, int sectionLevel, bool hasOverlap, ChunkingConfig config)
    {
        var tags = document.Metadata.TryGetValue("tags", out var tagStr)
            ? tagStr.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList()
            : new List<string>();

        return new DocumentChunk
        {
            DocumentId = document.Id,
            Content = content,
            TokenCount = EstimateTokens(content),
            Metadata = new ChunkMetadata
            {
                Source = document.Source,
                FileName = document.FileName,
                Section = sectionTitle,
                SectionLevel = sectionLevel,
                ContentType = config.ContentType,
                Collection = config.Collection,
                DocumentHash = document.ContentHash,
                HasOverlap = hasOverlap,
                Tags = tags,
                DocumentDate = document.ParsedAt
            }
        };
    }

    private static string BuildSectionText(DocumentSection section)
    {
        if (string.IsNullOrEmpty(section.Title))
            return section.Content;

        var prefix = section.Level > 0 ? new string('#', section.Level) + " " : "";
        return $"{prefix}{section.Title}\n{section.Content}";
    }

    private static List<string> SplitIntoSentences(string text)
    {
        var sentences = new List<string>();
        var current = new System.Text.StringBuilder();

        foreach (var ch in text)
        {
            current.Append(ch);
            if (ch is '.' or '!' or '?' or '\n')
            {
                var sentence = current.ToString().Trim();
                if (!string.IsNullOrWhiteSpace(sentence))
                    sentences.Add(sentence);
                current.Clear();
            }
        }

        var remaining = current.ToString().Trim();
        if (!string.IsNullOrWhiteSpace(remaining))
            sentences.Add(remaining);

        return sentences;
    }

    /// <summary>
    /// Estimativa rápida de tokens (~4 chars/token para inglês/português).
    /// </summary>
    internal static int EstimateTokens(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        return (int)Math.Ceiling(text.Length / 4.0);
    }
}
