using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Interfaces;

/// <summary>
/// Persistência operacional de artefatos, eventos, métricas e reflexões do runtime.
/// </summary>
public interface IOperationalStore
{
    // ── Artifacts ──────────────────────────────────────────
    Task SaveArtifactAsync(AgentExecutionArtifact artifact, CancellationToken ct = default);
    Task<IReadOnlyList<AgentExecutionArtifact>> GetArtifactsAsync(string sessionId, CancellationToken ct = default);
    Task<IReadOnlyList<AgentExecutionArtifact>> QueryArtifactsAsync(
        string? sessionId = null,
        AgentExecutionArtifactType? type = null,
        DateTime? from = null,
        DateTime? to = null,
        int limit = 100,
        CancellationToken ct = default);

    // ── Metrics Snapshots ─────────────────────────────────
    Task SaveMetricsSnapshotAsync(AgentRuntimeMetricsSnapshot snapshot, CancellationToken ct = default);
    Task<AgentRuntimeMetricsSnapshot?> GetLatestMetricsAsync(string? sessionId = null, CancellationToken ct = default);
    Task<IReadOnlyList<AgentRuntimeMetricsSnapshot>> GetMetricsHistoryAsync(
        string? sessionId = null,
        DateTime? from = null,
        DateTime? to = null,
        int limit = 100,
        CancellationToken ct = default);

    // ── Reflections ───────────────────────────────────────
    Task SaveReflectionAsync(Reflection reflection, CancellationToken ct = default);
    Task<IReadOnlyList<Reflection>> GetReflectionsAsync(string sessionId, CancellationToken ct = default);
    Task<IReadOnlyList<Reflection>> GetRecentLearningsAsync(int count = 10, CancellationToken ct = default);

    // ── Evaluation Scores ─────────────────────────────────
    Task SaveEvaluationAsync(RuntimeEvaluationResult evaluation, CancellationToken ct = default);
    Task<IReadOnlyList<RuntimeEvaluationResult>> GetEvaluationsAsync(
        string? sessionId = null,
        string? agentName = null,
        DateTime? from = null,
        DateTime? to = null,
        int limit = 50,
        CancellationToken ct = default);
    Task<RuntimeEvaluationResult?> GetLatestEvaluationAsync(string? agentName = null, CancellationToken ct = default);
}

/// <summary>
/// Resultado de avaliação contínua do runtime evaluator.
/// </summary>
public class RuntimeEvaluationResult
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string? SessionId { get; set; }
    public string? AgentName { get; set; }
    public double OverallScore { get; set; }
    public double BaselineScore { get; set; }
    public double Threshold { get; set; }
    public bool RegressionDetected { get; set; }
    public Dictionary<string, double> Factors { get; set; } = new();
    public List<string> Alerts { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
