using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Interfaces;

/// <summary>
/// Advanced retrieval strategies extending the base RAG pipeline.
/// Supports hybrid search (vector + keyword), multi-query decomposition,
/// parent-child chunk relationships, and self-corrective RAG.
/// </summary>
public interface IAdvancedRetrievalService
{
    /// <summary>
    /// Performs hybrid search combining vector similarity with keyword (BM25) scoring.
    /// Returns merged and de-duplicated results.
    /// </summary>
    Task<RAGContext> HybridSearchAsync(
        RAGQuery query,
        HybridSearchOptions? options = null,
        CancellationToken ct = default);

    /// <summary>
    /// Decomposes a complex query into multiple sub-queries,
    /// executes each independently, and merges the results.
    /// </summary>
    Task<RAGContext> MultiQueryRetrieveAsync(
        RAGQuery query,
        CancellationToken ct = default);

    /// <summary>
    /// Self-RAG: evaluates retrieval relevance and iteratively refines
    /// the query if initial results are insufficient.
    /// </summary>
    Task<RAGContext> SelfCorrectiveRetrieveAsync(
        RAGQuery query,
        double relevanceThreshold = 0.5,
        int maxIterations = 3,
        CancellationToken ct = default);

    /// <summary>
    /// Resolves parent-child chunk relationships. When a child chunk matches,
    /// retrieves the parent chunk for broader context.
    /// </summary>
    Task<IReadOnlyList<RankedChunk>> ResolveParentChunksAsync(
        IReadOnlyList<RankedChunk> childChunks,
        CancellationToken ct = default);
}

/// <summary>
/// Options for hybrid search weighting.
/// </summary>
public class HybridSearchOptions
{
    /// <summary>
    /// Weight for vector similarity (0.0 - 1.0). Default 0.7 (70% vector).
    /// </summary>
    public double VectorWeight { get; init; } = 0.7;

    /// <summary>
    /// Weight for keyword/BM25 scoring (0.0 - 1.0). Default 0.3 (30% keyword).
    /// </summary>
    public double KeywordWeight { get; init; } = 0.3;

    /// <summary>
    /// When true, applies Reciprocal Rank Fusion to merge results from both sources.
    /// </summary>
    public bool UseRRF { get; init; } = true;

    /// <summary>
    /// RRF constant K (typically 60). Higher values flatten ranking differences.
    /// </summary>
    public int RrfK { get; init; } = 60;
}
