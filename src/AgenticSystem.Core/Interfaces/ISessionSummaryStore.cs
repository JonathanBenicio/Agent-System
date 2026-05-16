using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Interfaces;

public interface ISessionSummaryStore
{
    Task SaveSummaryAsync(SessionSummary summary, string userId, string tenantId, CancellationToken ct = default);
    Task SaveInsightsAsync(SessionInsights insights, string userId, string tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<SessionSummary>> GetRelevantAsync(string query, int maxResults = 5, CancellationToken ct = default);
}
