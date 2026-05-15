using System.Text.Json;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Infrastructure.Persistence;

/// <summary>
/// Implementação PostgreSQL do operational store para artefatos, métricas, reflexões e avaliações.
/// </summary>
public class PostgresOperationalStore : IOperationalStore
{
    private readonly IDbContextFactory<AgenticDbContext> _dbContextFactory;
    private readonly ILogger<PostgresOperationalStore> _logger;

    public PostgresOperationalStore(IDbContextFactory<AgenticDbContext> dbContextFactory, ILogger<PostgresOperationalStore> logger)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    // ── Artifacts ──────────────────────────────────────────

    public async Task SaveArtifactAsync(AgentExecutionArtifact artifact, CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var entity = new RuntimeArtifactEntity
        {
            Id = artifact.Id,
            SessionId = artifact.SessionId,
            Type = artifact.Type.ToString(),
            Name = artifact.Name,
            AgentName = artifact.AgentName,
            Status = artifact.Status,
            Summary = artifact.Summary,
            DataJson = JsonSerializer.Serialize(artifact.Data),
            RelatedIdsJson = JsonSerializer.Serialize(artifact.RelatedIds),
            CreatedAt = artifact.CreatedAt
        };

        db.RuntimeArtifacts.Add(entity);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<AgentExecutionArtifact>> GetArtifactsAsync(string sessionId, CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var entities = await db.RuntimeArtifacts
            .AsNoTracking()
            .Where(a => a.SessionId == sessionId)
            .OrderBy(a => a.CreatedAt)
            .ToListAsync(ct);

        return entities.Select(MapToArtifact).ToList();
    }

    public async Task<IReadOnlyList<AgentExecutionArtifact>> QueryArtifactsAsync(
        string? sessionId = null,
        AgentExecutionArtifactType? type = null,
        DateTime? from = null,
        DateTime? to = null,
        int limit = 100,
        CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var query = db.RuntimeArtifacts.AsNoTracking().AsQueryable();

        if (sessionId is not null)
            query = query.Where(a => a.SessionId == sessionId);
        if (type is not null)
            query = query.Where(a => a.Type == type.Value.ToString());
        if (from is not null)
            query = query.Where(a => a.CreatedAt >= from.Value);
        if (to is not null)
            query = query.Where(a => a.CreatedAt <= to.Value);

        var entities = await query
            .OrderByDescending(a => a.CreatedAt)
            .Take(limit)
            .ToListAsync(ct);

        return entities.Select(MapToArtifact).ToList();
    }

    // ── Metrics Snapshots ─────────────────────────────────

    public async Task SaveMetricsSnapshotAsync(AgentRuntimeMetricsSnapshot snapshot, CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var entity = new RuntimeMetricsSnapshotEntity
        {
            SessionId = snapshot.SessionId,
            StreamCount = snapshot.StreamCount,
            AgentExecutions = snapshot.AgentExecutions,
            AgentFallbacks = snapshot.AgentFallbacks,
            ToolExecutions = snapshot.ToolExecutions,
            ToolApprovalsRequested = snapshot.ToolApprovalsRequested,
            ToolApprovalsResolved = snapshot.ToolApprovalsResolved,
            FinalApprovalsRequested = snapshot.FinalApprovalsRequested,
            FinalApprovalsResolved = snapshot.FinalApprovalsResolved,
            Handoffs = snapshot.Handoffs,
            RagQueries = snapshot.RagQueries,
            Reviews = snapshot.Reviews,
            AverageAgentLatencyMs = snapshot.AverageAgentLatencyMs,
            AverageToolLatencyMs = snapshot.AverageToolLatencyMs,
            EventsByTypeJson = JsonSerializer.Serialize(snapshot.EventsByType),
            AgentExecutionCountsJson = JsonSerializer.Serialize(snapshot.AgentExecutionCounts),
            SnapshotAt = snapshot.UpdatedAt
        };

        db.RuntimeMetricsSnapshots.Add(entity);
        await db.SaveChangesAsync(ct);
    }

    public async Task<AgentRuntimeMetricsSnapshot?> GetLatestMetricsAsync(string? sessionId = null, CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var query = db.RuntimeMetricsSnapshots.AsNoTracking().AsQueryable();

        if (sessionId is not null)
            query = query.Where(m => m.SessionId == sessionId);

        var entity = await query.OrderByDescending(m => m.SnapshotAt).FirstOrDefaultAsync(ct);
        return entity is null ? null : MapToSnapshot(entity);
    }

    public async Task<IReadOnlyList<AgentRuntimeMetricsSnapshot>> GetMetricsHistoryAsync(
        string? sessionId = null,
        DateTime? from = null,
        DateTime? to = null,
        int limit = 100,
        CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var query = db.RuntimeMetricsSnapshots.AsNoTracking().AsQueryable();

        if (sessionId is not null)
            query = query.Where(m => m.SessionId == sessionId);
        if (from is not null)
            query = query.Where(m => m.SnapshotAt >= from.Value);
        if (to is not null)
            query = query.Where(m => m.SnapshotAt <= to.Value);

        var entities = await query
            .OrderByDescending(m => m.SnapshotAt)
            .Take(limit)
            .ToListAsync(ct);

        return entities.Select(MapToSnapshot).ToList();
    }

    // ── Reflections ───────────────────────────────────────

    public async Task SaveReflectionAsync(Reflection reflection, CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var entity = new ReflectionEntity
        {
            Id = reflection.Id,
            SessionId = reflection.SessionId,
            AgentName = reflection.AgentName,
            ActionTaken = reflection.ActionTaken,
            Outcome = reflection.Outcome,
            ConfidenceInOutcome = reflection.ConfidenceInOutcome,
            DeviationsJson = JsonSerializer.Serialize(reflection.Deviations),
            LessonsLearnedJson = JsonSerializer.Serialize(reflection.LessonsLearned),
            ImprovementSuggestion = reflection.ImprovementSuggestion,
            Severity = reflection.Severity.ToString(),
            CreatedAt = reflection.CreatedAt
        };

        db.Reflections.Add(entity);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<Reflection>> GetReflectionsAsync(string sessionId, CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var entities = await db.Reflections
            .AsNoTracking()
            .Where(r => r.SessionId == sessionId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);

        return entities.Select(MapToReflection).ToList();
    }

    public async Task<IReadOnlyList<Reflection>> GetRecentLearningsAsync(int count = 10, CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var entities = await db.Reflections
            .AsNoTracking()
            .Where(r => r.LessonsLearnedJson != "[]")
            .OrderByDescending(r => r.CreatedAt)
            .Take(count)
            .ToListAsync(ct);

        return entities.Select(MapToReflection).ToList();
    }

    public async Task<IReadOnlyList<Reflection>> GetReflectionsSinceAsync(string? lastReflectionId, int limit = 100, CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var query = db.Reflections.AsNoTracking().AsQueryable();

        if (!string.IsNullOrEmpty(lastReflectionId))
        {
            var last = await db.Reflections.AsNoTracking().FirstOrDefaultAsync(r => r.Id == lastReflectionId, ct);
            if (last != null)
            {
                query = query.Where(r => r.CreatedAt > last.CreatedAt);
            }
        }

        var entities = await query
            .OrderBy(r => r.CreatedAt)
            .Take(limit)
            .ToListAsync(ct);

        return entities.Select(MapToReflection).ToList();
    }

    // ── Evaluation Scores ─────────────────────────────────

    public async Task SaveEvaluationAsync(RuntimeEvaluationResult evaluation, CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var entity = new EvaluationScoreEntity
        {
            Id = evaluation.Id,
            SessionId = evaluation.SessionId,
            AgentName = evaluation.AgentName,
            OverallScore = evaluation.OverallScore,
            BaselineScore = evaluation.BaselineScore,
            Threshold = evaluation.Threshold,
            RegressionDetected = evaluation.RegressionDetected,
            FactorsJson = JsonSerializer.Serialize(evaluation.Factors),
            AlertsJson = JsonSerializer.Serialize(evaluation.Alerts),
            CreatedAt = evaluation.CreatedAt
        };

        db.EvaluationScores.Add(entity);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<RuntimeEvaluationResult>> GetEvaluationsAsync(
        string? sessionId = null,
        string? agentName = null,
        DateTime? from = null,
        DateTime? to = null,
        int limit = 50,
        CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var query = db.EvaluationScores.AsNoTracking().AsQueryable();

        if (sessionId is not null)
            query = query.Where(e => e.SessionId == sessionId);
        if (agentName is not null)
            query = query.Where(e => e.AgentName == agentName);
        if (from is not null)
            query = query.Where(e => e.CreatedAt >= from.Value);
        if (to is not null)
            query = query.Where(e => e.CreatedAt <= to.Value);

        var entities = await query
            .OrderByDescending(e => e.CreatedAt)
            .Take(limit)
            .ToListAsync(ct);

        return entities.Select(MapToEvaluation).ToList();
    }

    public async Task<RuntimeEvaluationResult?> GetLatestEvaluationAsync(string? agentName = null, CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var query = db.EvaluationScores.AsNoTracking().AsQueryable();

        if (agentName is not null)
            query = query.Where(e => e.AgentName == agentName);

        var entity = await query.OrderByDescending(e => e.CreatedAt).FirstOrDefaultAsync(ct);
        return entity is null ? null : MapToEvaluation(entity);
    }

    // ── System State ──────────────────────────────────────

    public Task<SystemState?> GetSystemStateAsync(string id, CancellationToken ct = default)
    {
        // TODO: Implementar persistência de SystemState no Postgres
        return Task.FromResult<SystemState?>(null);
    }

    public Task SaveSystemStateAsync(SystemState state, CancellationToken ct = default)
    {
        // TODO: Implementar persistência de SystemState no Postgres
        return Task.CompletedTask;
    }

    // ── Mapping helpers ───────────────────────────────────

    private static AgentExecutionArtifact MapToArtifact(RuntimeArtifactEntity e) => new()
    {
        Id = e.Id,
        SessionId = e.SessionId,
        Type = Enum.TryParse<AgentExecutionArtifactType>(e.Type, out var t) ? t : AgentExecutionArtifactType.Step,
        Name = e.Name,
        AgentName = e.AgentName,
        Status = e.Status,
        Summary = e.Summary,
        Data = JsonSerializer.Deserialize<Dictionary<string, object>>(e.DataJson) ?? new(),
        RelatedIds = JsonSerializer.Deserialize<List<string>>(e.RelatedIdsJson) ?? new(),
        CreatedAt = e.CreatedAt
    };

    private static AgentRuntimeMetricsSnapshot MapToSnapshot(RuntimeMetricsSnapshotEntity e) => new()
    {
        SessionId = e.SessionId,
        StreamCount = e.StreamCount,
        AgentExecutions = e.AgentExecutions,
        AgentFallbacks = e.AgentFallbacks,
        ToolExecutions = e.ToolExecutions,
        ToolApprovalsRequested = e.ToolApprovalsRequested,
        ToolApprovalsResolved = e.ToolApprovalsResolved,
        FinalApprovalsRequested = e.FinalApprovalsRequested,
        FinalApprovalsResolved = e.FinalApprovalsResolved,
        Handoffs = e.Handoffs,
        RagQueries = e.RagQueries,
        Reviews = e.Reviews,
        AverageAgentLatencyMs = e.AverageAgentLatencyMs,
        AverageToolLatencyMs = e.AverageToolLatencyMs,
        EventsByType = JsonSerializer.Deserialize<Dictionary<string, long>>(e.EventsByTypeJson) ?? new(),
        AgentExecutionCounts = JsonSerializer.Deserialize<Dictionary<string, long>>(e.AgentExecutionCountsJson) ?? new(),
        UpdatedAt = e.SnapshotAt
    };

    private static Reflection MapToReflection(ReflectionEntity e) => new()
    {
        Id = e.Id,
        SessionId = e.SessionId,
        AgentName = e.AgentName,
        ActionTaken = e.ActionTaken,
        Outcome = e.Outcome,
        ConfidenceInOutcome = e.ConfidenceInOutcome,
        Deviations = JsonSerializer.Deserialize<List<string>>(e.DeviationsJson) ?? new(),
        LessonsLearned = JsonSerializer.Deserialize<List<string>>(e.LessonsLearnedJson) ?? new(),
        ImprovementSuggestion = e.ImprovementSuggestion,
        Severity = Enum.TryParse<ReflectionSeverity>(e.Severity, out var s) ? s : ReflectionSeverity.Info,
        CreatedAt = e.CreatedAt
    };

    private static RuntimeEvaluationResult MapToEvaluation(EvaluationScoreEntity e) => new()
    {
        Id = e.Id,
        SessionId = e.SessionId,
        AgentName = e.AgentName,
        OverallScore = e.OverallScore,
        BaselineScore = e.BaselineScore,
        Threshold = e.Threshold,
        RegressionDetected = e.RegressionDetected,
        Factors = JsonSerializer.Deserialize<Dictionary<string, double>>(e.FactorsJson) ?? new(),
        Alerts = JsonSerializer.Deserialize<List<string>>(e.AlertsJson) ?? new(),
        CreatedAt = e.CreatedAt
    };
}
