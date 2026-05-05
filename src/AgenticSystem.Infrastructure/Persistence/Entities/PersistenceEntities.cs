namespace AgenticSystem.Infrastructure.Persistence.Entities;

/// <summary>
/// Entidade de documento vetorial para persistência PostgreSQL + pgvector.
/// </summary>
public class VectorDocumentEntity
{
    public string Id { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Collection { get; set; } = string.Empty;
    public float[] Embedding { get; set; } = [];
    public string MetadataJson { get; set; } = "{}";
    public DateTime IndexedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Entidade de entrada de custo para persistência PostgreSQL.
/// </summary>
public class CostEntryEntity
{
    public long Id { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string TenantId { get; set; } = "default";
    public decimal Cost { get; set; }
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Entidade de orçamento diário por serviço/tenant.
/// </summary>
public class CostBudgetEntity
{
    public string Id { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty;
    public string TenantId { get; set; } = "default";
    public decimal DailyBudget { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Entidade de métrica de performance de agente para persistência PostgreSQL.
/// </summary>
public class AgentPerformanceMetricEntity
{
    public long Id { get; set; }
    public string AgentName { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public double LatencyMs { get; set; }
    public bool Success { get; set; }
    public double? UserSatisfaction { get; set; }
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
}

// ═══════════════════════════════════════════════════════════
// Operational Store Entities
// ═══════════════════════════════════════════════════════════

/// <summary>
/// Artefato de execução persistido.
/// </summary>
public class RuntimeArtifactEntity
{
    public string Id { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? AgentName { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string DataJson { get; set; } = "{}";
    public string RelatedIdsJson { get; set; } = "[]";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Snapshot periódico de métricas do runtime.
/// </summary>
public class RuntimeMetricsSnapshotEntity
{
    public long Id { get; set; }
    public string? SessionId { get; set; }
    public long StreamCount { get; set; }
    public long AgentExecutions { get; set; }
    public long AgentFallbacks { get; set; }
    public long ToolExecutions { get; set; }
    public long ToolApprovalsRequested { get; set; }
    public long ToolApprovalsResolved { get; set; }
    public long FinalApprovalsRequested { get; set; }
    public long FinalApprovalsResolved { get; set; }
    public long Handoffs { get; set; }
    public long RagQueries { get; set; }
    public long Reviews { get; set; }
    public double AverageAgentLatencyMs { get; set; }
    public double AverageToolLatencyMs { get; set; }
    public string EventsByTypeJson { get; set; } = "{}";
    public string AgentExecutionCountsJson { get; set; } = "{}";
    public DateTime SnapshotAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Reflexão pós-ação persistida.
/// </summary>
public class ReflectionEntity
{
    public string Id { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string AgentName { get; set; } = string.Empty;
    public string ActionTaken { get; set; } = string.Empty;
    public string Outcome { get; set; } = string.Empty;
    public double ConfidenceInOutcome { get; set; }
    public string DeviationsJson { get; set; } = "[]";
    public string LessonsLearnedJson { get; set; } = "[]";
    public string? ImprovementSuggestion { get; set; }
    public string Severity { get; set; } = "Info";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Resultado de avaliação contínua do runtime evaluator.
/// </summary>
public class EvaluationScoreEntity
{
    public string Id { get; set; } = string.Empty;
    public string? SessionId { get; set; }
    public string? AgentName { get; set; }
    public double OverallScore { get; set; }
    public double BaselineScore { get; set; }
    public double Threshold { get; set; }
    public bool RegressionDetected { get; set; }
    public string FactorsJson { get; set; } = "{}";
    public string AlertsJson { get; set; } = "[]";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Memória persistente por agente para reutilização entre sessões.
/// </summary>
public class AgentMemoryEntity
{
    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string AgentName { get; set; } = string.Empty;
    public string? SessionId { get; set; }
    public string MemoryType { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public string Source { get; set; } = "runtime";
    public string KeywordsJson { get; set; } = "[]";
    public string MetadataJson { get; set; } = "{}";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastUsedAt { get; set; } = DateTime.UtcNow;
    public int UsageCount { get; set; }
    public bool IsActive { get; set; } = true;
}
