using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Interfaces;

/// <summary>
/// Advanced query understanding service.
/// Extends IContextAnalyzer with query expansion, decomposition, and conversational resolution.
/// </summary>
public interface IQueryUnderstanding
{
    /// <summary>
    /// Decomposes a complex query into simpler sub-queries.
    /// </summary>
    Task<QueryDecomposition> DecomposeQueryAsync(
        string query,
        CancellationToken ct = default);

    /// <summary>
    /// Expands a query with synonyms, related terms, and contextual variants.
    /// </summary>
    Task<QueryExpansion> ExpandQueryAsync(
        string query,
        CancellationToken ct = default);

    /// <summary>
    /// Resolves conversational references (pronouns, ellipsis) using conversation history.
    /// </summary>
    Task<string> ResolveConversationalAsync(
        string currentQuery,
        IReadOnlyList<ConversationTurn> history,
        CancellationToken ct = default);

    /// <summary>
    /// Full pipeline: decompose → expand → resolve → produce final query set.
    /// </summary>
    Task<QueryUnderstandingResult> AnalyzeAsync(
        string query,
        IReadOnlyList<ConversationTurn>? history = null,
        CancellationToken ct = default);
}

/// <summary>
/// Result of query decomposition into sub-queries.
/// </summary>
public class QueryDecomposition
{
    public string OriginalQuery { get; init; } = string.Empty;
    public List<SubQuery> SubQueries { get; init; } = [];
    public bool IsComplex { get; init; }
}

public class SubQuery
{
    public string Query { get; init; } = string.Empty;
    public string Intent { get; init; } = string.Empty;
    public int Priority { get; init; }
    public List<string> Dependencies { get; init; } = [];
}

/// <summary>
/// Result of query expansion.
/// </summary>
public class QueryExpansion
{
    public string OriginalQuery { get; init; } = string.Empty;
    public List<string> ExpandedVariants { get; init; } = [];
    public List<string> Synonyms { get; init; } = [];
    public List<string> RelatedTerms { get; init; } = [];
}

/// <summary>
/// A turn in conversation history for context resolution.
/// </summary>
public class ConversationTurn
{
    public string Role { get; init; } = string.Empty; // "user" | "assistant"
    public string Content { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Complete result from the query understanding pipeline.
/// </summary>
public class QueryUnderstandingResult
{
    public string OriginalQuery { get; init; } = string.Empty;
    public string ResolvedQuery { get; init; } = string.Empty;
    public QueryDecomposition Decomposition { get; init; } = new();
    public QueryExpansion Expansion { get; init; } = new();
    public bool WasRewritten { get; init; }
    public string? Intent { get; init; }
    public double Confidence { get; init; }
}
