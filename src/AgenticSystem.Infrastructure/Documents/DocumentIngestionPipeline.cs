using System.Diagnostics;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Infrastructure.Documents;

using TextEmbeddingGenerator = Microsoft.Extensions.AI.IEmbeddingGenerator<string, Microsoft.Extensions.AI.Embedding<float>>;

/// <summary>
/// Pipeline de ingestão: detect type → parse → chunk → embed → index no VectorStore.
/// </summary>
public class DocumentIngestionPipeline : IDocumentIngestionPipeline
{
    private readonly Dictionary<DocumentType, IDocumentParser> _parsers;
    private readonly IChunkingStrategy _chunkingStrategy;
    private readonly TextEmbeddingGenerator? _embeddingGenerator;
    private readonly IVectorStore _vectorStore;
    private readonly IMultimodalProcessor? _multimodalProcessor;
    private readonly ITenantIsolationEnforcer? _isolationEnforcer;
    private readonly IChatClient? _chatClient;
    private readonly ILogger<DocumentIngestionPipeline> _logger;

    public DocumentIngestionPipeline(
        IEnumerable<IDocumentParser> parsers,
        IChunkingStrategy chunkingStrategy,
        TextEmbeddingGenerator? embeddingGenerator,
        IVectorStore vectorStore,
        ILogger<DocumentIngestionPipeline> logger,
        IMultimodalProcessor? multimodalProcessor = null,
        ITenantIsolationEnforcer? isolationEnforcer = null,
        IChatClient? chatClient = null)
    {
        _parsers = parsers.ToDictionary(p => p.SupportedType);
        _chunkingStrategy = chunkingStrategy;
        _embeddingGenerator = embeddingGenerator;
        _vectorStore = vectorStore;
        _logger = logger;
        _multimodalProcessor = multimodalProcessor;
        _isolationEnforcer = isolationEnforcer;
        _chatClient = chatClient;
    }

    public async Task<IngestionResult> IngestAsync(
        RawDocument document, ChunkingConfig? config = null, CancellationToken ct = default)
    {
        config ??= new ChunkingConfig();

        // Enforce tenant ingestion limits
        if (_isolationEnforcer != null && !string.IsNullOrEmpty(config.TenantId))
        {
            if (!await _isolationEnforcer.CanIngestDocumentAsync(config.TenantId, ct: ct))
            {
                return IngestionResult.Fail(document.Id, document.FileName, "🚫 Limite de documentos atingido para o seu tenant.");
            }
        }

        var sw = Stopwatch.StartNew();
        var chunks = new List<DocumentChunk>();
        var contentHash = string.Empty;

        try
        {
            // 1. Multimodal processing for images/audio
            if ((document.Type == DocumentType.Image || document.Type == DocumentType.Audio) && _multimodalProcessor != null)
            {
                var mimeType = GetMimeType(document.FileName);
                using var ms = new MemoryStream(document.Content);
                var multimodalDoc = await _multimodalProcessor.ProcessAsync(ms, document.FileName, mimeType, ct);
                
                contentHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(document.Content)).ToLowerInvariant();

                for (int i = 0; i < multimodalDoc.ExtractedContents.Count; i++)
                {
                    var content = multimodalDoc.ExtractedContents[i];
                    chunks.Add(new DocumentChunk
                    {
                        Id = $"{document.Id}_{i}",
                        DocumentId = document.Id,
                        Content = content.Content,
                        Index = i,
                        TokenCount = EstimateTokens(content.Content),
                        Metadata = new ChunkMetadata
                        {
                            Source = document.Source,
                            FileName = document.FileName,
                            ContentType = content.ExtractionType.ToString(),
                            Collection = config.Collection,
                            DocumentHash = contentHash,
                            ChunkIndex = i,
                            TotalChunks = multimodalDoc.ExtractedContents.Count
                        }
                    });
                }
            }
            // 2. Standard Parsing for Text/PDF/DOCX
            else
            {
                if (!_parsers.TryGetValue(document.Type, out var parser))
                {
                    return IngestionResult.Fail(document.Id, document.FileName,
                        $"No parser registered for type {document.Type}");
                }

                var parsed = await parser.ParseAsync(document, ct);
                contentHash = parsed.ContentHash;
                chunks = (await _chunkingStrategy.ChunkAsync(parsed, config, ct)).ToList();
            }

            if (chunks.Count == 0)
            {
                return IngestionResult.Fail(document.Id, document.FileName,
                    "Document produced 0 chunks after parsing or processing");
            }

            if (_embeddingGenerator is null)
            {
                return IngestionResult.Fail(document.Id, document.FileName,
                    "No embedding generator is configured for document ingestion");
            }

            // 2.5 Contextual Retrieval (Enrichment)
            if (_chatClient != null && chunks.Count > 0 && document.Type != DocumentType.Image)
            {
                var docContent = string.Join("\n\n", chunks.Select(c => c.Content));
                if (docContent.Length > 25000) docContent = docContent[..25000]; // Protect against huge context limits

                var summaryTasks = chunks.Select(async chunk =>
                {
                    var prompt = $@"
You are an expert at information retrieval. Here is a document:
<document>
{docContent}
</document>

Here is a chunk extracted from it:
<chunk>
{chunk.Content}
</chunk>

Please give a short, concise context of this chunk within the overall document (1 to 2 sentences). Do not repeat the chunk. Just provide the context.";

                    try
                    {
                        var response = await _chatClient.GetResponseAsync(new List<ChatMessage> { new(ChatRole.User, prompt) }, cancellationToken: ct);
                        chunk.ContextualSummary = response.Text?.Trim();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to generate contextual summary for chunk {Index} of {File}", chunk.Index, document.FileName);
                    }
                });

                await Task.WhenAll(summaryTasks);
            }

            // 3. Embed (batch)
            // If ContextualSummary exists, we prepend it to the content being embedded to enrich the vector representation
            var texts = chunks.Select(c => string.IsNullOrEmpty(c.ContextualSummary) ? c.Content : $"{c.ContextualSummary}\n\n{c.Content}").ToList();
            var embeddings = await _embeddingGenerator.GenerateAsync(texts, cancellationToken: ct);
            var embeddingVectors = embeddings.Select(item => item.Vector.ToArray()).ToList();

            // 4. Index no VectorStore
            var totalTokens = 0;
            for (var i = 0; i < chunks.Count; i++)
            {
                var chunk = chunks[i];
                chunk.Embedding = embeddingVectors[i];
                totalTokens += chunk.TokenCount;

                var embDoc = chunk.ToEmbeddingDocument();
                await _vectorStore.UpsertAsync(embDoc);
            }

            sw.Stop();

            _logger.LogInformation(
                "✅ Ingested {File}: {Chunks} chunks, {Tokens} tokens, hash={Hash} in {Ms}ms",
                document.FileName, chunks.Count, totalTokens, contentHash.Length > 8 ? contentHash[..8] : contentHash, sw.ElapsedMilliseconds);

            return IngestionResult.Ok(document.Id, document.FileName,
                chunks.Count, totalTokens, contentHash, sw.Elapsed);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "❌ Ingestion failed for {File}", document.FileName);
            return IngestionResult.Fail(document.Id, document.FileName, ex.Message);
        }
    }

    public async Task<IReadOnlyList<IngestionResult>> IngestBatchAsync(
        IEnumerable<RawDocument> documents, ChunkingConfig? config = null, CancellationToken ct = default)
    {
        var results = new List<IngestionResult>();
        foreach (var doc in documents)
        {
            ct.ThrowIfCancellationRequested();
            var result = await IngestAsync(doc, config, ct);
            results.Add(result);
        }

        var succeeded = results.Count(r => r.Success);
        var failed = results.Count(r => !r.Success);
        _logger.LogInformation("📦 Batch ingestion: {Ok} succeeded, {Fail} failed out of {Total}",
            succeeded, failed, results.Count);

        return results;
    }

    private string GetMimeType(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".mp3" => "audio/mpeg",
            ".wav" => "audio/wav",
            _ => "application/octet-stream"
        };
    }

    private int EstimateTokens(string text)
    {
        // Simple estimation: 1 token ~= 4 chars
        return string.IsNullOrEmpty(text) ? 0 : text.Length / 4;
    }
}
