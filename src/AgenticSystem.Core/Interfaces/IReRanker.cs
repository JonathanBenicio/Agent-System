using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Interfaces;

/// <summary>
/// Re-ranker — reordena resultados de busca vetorial por relevância contextual.
/// </summary>
public interface IReRanker
{
    Task<IReadOnlyList<RankedChunk>> ReRankAsync(string query, IReadOnlyList<SearchMatch> candidates, int topK = 5, CancellationToken ct = default);
}
