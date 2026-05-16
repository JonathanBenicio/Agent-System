using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.Persistence;
using AgenticSystem.Infrastructure.Persistence.Entities;

namespace AgenticSystem.Infrastructure.Persistence;

public class PostgresSessionSummaryStore : ISessionSummaryStore
{
    private readonly IDbContextFactory<AgenticDbContext> _dbContextFactory;
    private readonly ILogger<PostgresSessionSummaryStore> _logger;

    public PostgresSessionSummaryStore(
        IDbContextFactory<AgenticDbContext> dbContextFactory,
        ILogger<PostgresSessionSummaryStore> logger)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    public async Task SaveSummaryAsync(SessionSummary summary, string userId, string tenantId, CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);

        var entity = new SessionSummaryEntity
        {
            Id = Guid.NewGuid().ToString(),
            SessionId = summary.SessionId,
            UserId = userId,
            TenantId = tenantId,
            Summary = summary.Summary,
            TopicsJson = JsonSerializer.Serialize(summary.TopicsDiscussed),
            AgentsJson = JsonSerializer.Serialize(summary.AgentsUsed),
            EventCount = summary.EventCount,
            CreatedAt = summary.CreatedAt,
            SessionDuration = summary.SessionDuration,
        };

        db.SessionSummaries.Add(entity);
        await db.SaveChangesAsync(ct);

        _logger.LogInformation("💾 Session summary persisted for session {SessionId}", summary.SessionId);
    }

    public async Task SaveInsightsAsync(SessionInsights insights, string userId, string tenantId, CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);

        var entity = new SessionInsightEntity
        {
            Id = Guid.NewGuid().ToString(),
            SessionId = insights.SessionId,
            UserId = userId,
            TenantId = tenantId,
            FactsJson = JsonSerializer.Serialize(insights.Facts),
            DecisionsJson = JsonSerializer.Serialize(insights.Decisions),
            PreferencesJson = JsonSerializer.Serialize(insights.Preferences),
            ActionItemsJson = JsonSerializer.Serialize(insights.ActionItems),
            CreatedAt = DateTime.UtcNow,
        };

        db.SessionInsights.Add(entity);
        await db.SaveChangesAsync(ct);

        _logger.LogInformation("💾 Session insights persisted for session {SessionId}", insights.SessionId);
    }

    public async Task<IReadOnlyList<SessionSummary>> GetRelevantAsync(string query, int maxResults = 5, CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);

        var queryTerms = query.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var summaries = await db.SessionSummaries
            .OrderByDescending(s => s.CreatedAt)
            .Take(maxResults * 3)
            .ToListAsync(ct);

        var relevant = summaries
            .Select(s => new SessionSummary
            {
                SessionId = s.SessionId,
                Summary = s.Summary,
                TopicsDiscussed = JsonSerializer.Deserialize<List<string>>(s.TopicsJson) ?? [],
                AgentsUsed = JsonSerializer.Deserialize<List<string>>(s.AgentsJson) ?? [],
                EventCount = s.EventCount,
                CreatedAt = s.CreatedAt,
                SessionDuration = s.SessionDuration,
            })
            .OrderByDescending(s =>
            {
                var text = $"{s.Summary} {string.Join(" ", s.TopicsDiscussed)}".ToLowerInvariant();
                return queryTerms.Count(t => text.Contains(t));
            })
            .Take(maxResults)
            .ToList();

        return relevant;
    }
}
