using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace AgenticSystem.Infrastructure.Persistence;

/// <summary>
/// Implementação PostgreSQL de ISessionStore para produção.
/// Requer tabela 'sessions' criada via migration (Flyway/Liquibase).
/// </summary>
public class PostgresSessionStore : ISessionStore
{
    private readonly IDbContextFactory<AgenticDbContext> _dbContextFactory;
    private readonly ILogger<PostgresSessionStore> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public PostgresSessionStore(IDbContextFactory<AgenticDbContext> dbContextFactory, ILogger<PostgresSessionStore> logger)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    public async Task SaveAsync(SessionData session, CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var entity = await db.SessionRecords.IgnoreQueryFilters()
            .FirstOrDefaultAsync(record => record.Id == session.Id, ct);
        var serialized = JsonSerializer.Serialize(session, JsonOptions);

        if (entity is null)
        {
            db.SessionRecords.Add(new SessionRecordEntity
            {
                Id = session.Id,
                UserId = session.UserId,
                TenantId = session.TenantId,
                DataJson = serialized,
                StartedAt = session.StartedAt,
                EndedAt = session.EndedAt,
                IsConsolidated = session.IsConsolidated
            });
        }
        else
        {
            if (entity.UserId != session.UserId || entity.TenantId != session.TenantId)
                throw new InvalidOperationException("Session owner and tenant cannot be changed.");
            entity.DataJson = serialized;
            entity.StartedAt = session.StartedAt;
            entity.EndedAt = session.EndedAt;
            entity.IsConsolidated = session.IsConsolidated;
        }

        await db.SaveChangesAsync(ct);
        _logger.LogDebug("Session saved to PostgreSQL via EF Core: {SessionId}", session.Id);
    }

    public async Task<SessionData?> GetAsync(string sessionId, CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var json = await db.SessionRecords
            .AsNoTracking()
            .Where(record => record.Id == sessionId)
            .Select(record => record.DataJson)
            .FirstOrDefaultAsync(ct);

        if (json is null)
            return null;

        try
        {
            var session = JsonSerializer.Deserialize<SessionData>(json, JsonOptions);
            if (session is not null)
            {
                // Carregar resumo executivo se houver
                var summaryEntity = await db.SessionSummaries
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.SessionId == sessionId, ct);

                if (summaryEntity is not null)
                {
                    session.Summary = new SessionSummary
                    {
                        SessionId = summaryEntity.SessionId,
                        Summary = summaryEntity.Summary,
                        TopicsDiscussed = JsonSerializer.Deserialize<List<string>>(summaryEntity.TopicsJson, JsonOptions) ?? [],
                        AgentsUsed = JsonSerializer.Deserialize<List<string>>(summaryEntity.AgentsJson, JsonOptions) ?? [],
                        EventCount = summaryEntity.EventCount,
                        CreatedAt = summaryEntity.CreatedAt,
                        SessionDuration = summaryEntity.SessionDuration
                    };
                }

                // Carregar insights se houver
                var insightEntity = await db.SessionInsights
                    .AsNoTracking()
                    .FirstOrDefaultAsync(i => i.SessionId == sessionId, ct);

                if (insightEntity is not null)
                {
                    session.Insights = new SessionInsights
                    {
                        SessionId = insightEntity.SessionId,
                        Facts = JsonSerializer.Deserialize<List<string>>(insightEntity.FactsJson, JsonOptions) ?? [],
                        Decisions = JsonSerializer.Deserialize<List<string>>(insightEntity.DecisionsJson, JsonOptions) ?? [],
                        Preferences = JsonSerializer.Deserialize<List<string>>(insightEntity.PreferencesJson, JsonOptions) ?? [],
                        ActionItems = JsonSerializer.Deserialize<List<string>>(insightEntity.ActionItemsJson, JsonOptions) ?? []
                    };
                }
            }

            return session;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize session {SessionId}", sessionId);
            return null;
        }
    }

    public async Task<IReadOnlyList<SessionData>> GetByUserAsync(string userId, int maxResults = 10, string? search = null, CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var query = db.SessionRecords
            .AsNoTracking()
            .Where(record => record.UserId == userId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = $"%{search.Trim().ToLower()}%";
            query = query.Where(record =>
                EF.Functions.ILike(record.DataJson, searchLower) ||
                db.SessionSummaries.Any(s => s.SessionId == record.Id && EF.Functions.ILike(s.Summary, searchLower))
            );
        }

        var jsonRows = await query
            .OrderByDescending(record => record.StartedAt)
            .Take(maxResults)
            .Select(record => record.DataJson)
            .ToListAsync(ct);

        var sessions = DeserializeSessions(jsonRows);
        if (sessions.Count > 0)
        {
            var sessionIds = sessions.Select(s => s.Id).ToList();
            var summaries = await db.SessionSummaries
                .AsNoTracking()
                .Where(s => sessionIds.Contains(s.SessionId))
                .ToListAsync(ct);

            foreach (var session in sessions)
            {
                var summaryEntity = summaries.FirstOrDefault(s => s.SessionId == session.Id);
                if (summaryEntity is not null)
                {
                    session.Summary = new SessionSummary
                    {
                        SessionId = summaryEntity.SessionId,
                        Summary = summaryEntity.Summary,
                        TopicsDiscussed = JsonSerializer.Deserialize<List<string>>(summaryEntity.TopicsJson, JsonOptions) ?? [],
                        AgentsUsed = JsonSerializer.Deserialize<List<string>>(summaryEntity.AgentsJson, JsonOptions) ?? [],
                        EventCount = summaryEntity.EventCount,
                        CreatedAt = summaryEntity.CreatedAt,
                        SessionDuration = summaryEntity.SessionDuration
                    };
                }
            }
        }

        return sessions;
    }

    public async Task<IReadOnlyList<SessionData>> GetByTenantAsync(string tenantId, string? userId = null, int maxResults = 10, CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var query = db.SessionRecords
            .AsNoTracking()
            .Where(record => record.TenantId == tenantId);

        if (userId is not null)
        {
            query = query.Where(record => record.UserId == userId);
        }

        var jsonRows = await query
            .OrderByDescending(record => record.StartedAt)
            .Take(maxResults)
            .Select(record => record.DataJson)
            .ToListAsync(ct);

        var sessions = DeserializeSessions(jsonRows);
        if (sessions.Count > 0)
        {
            var sessionIds = sessions.Select(s => s.Id).ToList();
            var summaries = await db.SessionSummaries
                .AsNoTracking()
                .Where(s => sessionIds.Contains(s.SessionId))
                .ToListAsync(ct);

            foreach (var session in sessions)
            {
                var summaryEntity = summaries.FirstOrDefault(s => s.SessionId == session.Id);
                if (summaryEntity is not null)
                {
                    session.Summary = new SessionSummary
                    {
                        SessionId = summaryEntity.SessionId,
                        Summary = summaryEntity.Summary,
                        TopicsDiscussed = JsonSerializer.Deserialize<List<string>>(summaryEntity.TopicsJson, JsonOptions) ?? [],
                        AgentsUsed = JsonSerializer.Deserialize<List<string>>(summaryEntity.AgentsJson, JsonOptions) ?? [],
                        EventCount = summaryEntity.EventCount,
                        CreatedAt = summaryEntity.CreatedAt,
                        SessionDuration = summaryEntity.SessionDuration
                    };
                }
            }
        }

        return sessions;
    }

    public async Task DeleteAsync(string sessionId, CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var entity = await db.SessionRecords.FirstOrDefaultAsync(record => record.Id == sessionId, ct);
        if (entity is null)
        {
            return;
        }

        db.SessionRecords.Remove(entity);
        await db.SaveChangesAsync(ct);
        _logger.LogDebug("Session deleted from PostgreSQL via EF Core: {SessionId}", sessionId);
    }

    public async Task<bool> ExistsAsync(string sessionId, CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        return await db.SessionRecords.AsNoTracking().AnyAsync(record => record.Id == sessionId, ct);
    }

    private IReadOnlyList<SessionData> DeserializeSessions(IEnumerable<string> jsonRows)
    {
        var sessions = new List<SessionData>();

        foreach (var json in jsonRows)
        {
            try
            {
                var session = JsonSerializer.Deserialize<SessionData>(json, JsonOptions);
                if (session is not null)
                    sessions.Add(session);
            }
            catch (JsonException)
            {
                _logger.LogWarning("Skipping corrupted session payload while reading PostgreSQL records.");
            }
        }

        return sessions;
    }
}
