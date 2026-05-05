using AgenticSystem.Core.Models;

namespace AgenticSystem.Infrastructure.RAG;

public interface IDedicatedReRankerProvider
{
    string Name { get; }

    Task<DedicatedReRankingResult> ScoreAsync(
        string query,
        IReadOnlyList<RankedChunk> candidates,
        CancellationToken ct = default);
}

public sealed record DedicatedReRankingResult(IReadOnlyDictionary<string, double> Scores, string ProviderName)
{
    public static DedicatedReRankingResult Empty { get; } = new(
        new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase),
        "dedicated-provider-unavailable");
}