using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Interfaces;

/// <summary>
/// Pipeline completo de ingestão: parse → chunk → embed → index.
/// </summary>
public interface IDocumentIngestionPipeline
{
    Task<IngestionResult> IngestAsync(RawDocument document, ChunkingConfig? config = null, CancellationToken ct = default);
    Task<IReadOnlyList<IngestionResult>> IngestBatchAsync(IEnumerable<RawDocument> documents, ChunkingConfig? config = null, CancellationToken ct = default);
}
