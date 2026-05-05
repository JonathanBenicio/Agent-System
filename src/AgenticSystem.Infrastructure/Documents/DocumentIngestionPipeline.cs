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
    private readonly ILogger<DocumentIngestionPipeline> _logger;

    public DocumentIngestionPipeline(
        IEnumerable<IDocumentParser> parsers,
        IChunkingStrategy chunkingStrategy,
        TextEmbeddingGenerator? embeddingGenerator,
        IVectorStore vectorStore,
        ILogger<DocumentIngestionPipeline> logger)
    {
        _parsers = parsers.ToDictionary(p => p.SupportedType);
        _chunkingStrategy = chunkingStrategy;
        _embeddingGenerator = embeddingGenerator;
        _vectorStore = vectorStore;
        _logger = logger;
    }

    public async Task<IngestionResult> IngestAsync(
        RawDocument document, ChunkingConfig? config = null, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        config ??= new ChunkingConfig();

        try
        {
            // 1. Parse
            if (!_parsers.TryGetValue(document.Type, out var parser))
            {
                return IngestionResult.Fail(document.Id, document.FileName,
                    $"No parser registered for type {document.Type}");
            }

            var parsed = await parser.ParseAsync(document, ct);

            // 2. Chunk
            var chunks = await _chunkingStrategy.ChunkAsync(parsed, config, ct);

            if (chunks.Count == 0)
            {
                return IngestionResult.Fail(document.Id, document.FileName,
                    "Document produced 0 chunks after parsing");
            }

            if (_embeddingGenerator is null)
            {
                return IngestionResult.Fail(document.Id, document.FileName,
                    "No embedding generator is configured for document ingestion");
            }

            // 3. Embed (batch)
            var texts = chunks.Select(c => c.Content).ToList();
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
                document.FileName, chunks.Count, totalTokens, parsed.ContentHash[..8], sw.ElapsedMilliseconds);

            return IngestionResult.Ok(document.Id, document.FileName,
                chunks.Count, totalTokens, parsed.ContentHash, sw.Elapsed);
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
}
