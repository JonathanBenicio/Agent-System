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

public class SessionRecordEntity
{
    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string DataJson { get; set; } = "{}";
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public bool IsConsolidated { get; set; }
}

public class ConfigEntryEntity
{
    public string Id { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string? EncryptedValue { get; set; }
    public bool IsSecret { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Provider { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string MetadataJson { get; set; } = "{}";
}

public class ConfigChangeLogEntity
{
    public string Id { get; set; } = string.Empty;
    public string ConfigKey { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string? ChangedBy { get; set; }
    public DateTime ChangedAt { get; set; }
    public string? PreviousValueHash { get; set; }
    public string? NewValueHash { get; set; }
}

public class ScheduledTaskEntity
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? NextRunAt { get; set; }
    public string PayloadJson { get; set; } = "{}";
    public DateTime UpdatedAt { get; set; }
}

public class TriggerRuleEntity
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public string PayloadJson { get; set; } = "{}";
    public DateTime UpdatedAt { get; set; }
}

public class ScheduledTaskExecutionEntity
{
    public string ExecutionId { get; set; } = string.Empty;
    public string TaskId { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public bool Success { get; set; }
    public string PayloadJson { get; set; } = "{}";
}

public class EmbeddingModelEntity
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string DataJson { get; set; } = "{}";
    public DateTime CreatedAt { get; set; }
}

public class MigrationJobEntity
{
    public string Id { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string DataJson { get; set; } = "{}";
    public DateTime CreatedAt { get; set; }
}

public class RerankingAssetEntity
{
    public string TenantId { get; set; } = string.Empty;
    public string AssetType { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
    public byte[] Content { get; set; } = Array.Empty<byte>();
    public string ContentHash { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
}

// ═══════════════════════════════════════════════════════════
// Phase 2 Enterprise Entities (Audit, RBAC, Outbox)
// ═══════════════════════════════════════════════════════════

public class AuditEntryEntity
{
    public string Id { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Category { get; set; } = string.Empty; // Core.Models.AuditCategory
    public string Action { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public string? TenantId { get; set; }
    public string? SessionId { get; set; }
    public string? AgentName { get; set; }
    public string? ToolName { get; set; }
    public string? ModelUsed { get; set; }
    public decimal? Cost { get; set; }
    public string? TraceId { get; set; }
    public string? Description { get; set; }
    public bool Success { get; set; } = true;
    public string? ErrorMessage { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string DetailsJson { get; set; } = "{}";
}

public class RoleAssignmentEntity
{
    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string RoleId { get; set; } = string.Empty;
    public string? TenantId { get; set; }
    public string? GrantedBy { get; set; }
    public DateTime GrantedAt { get; set; } = DateTime.UtcNow;
}

public class OutboxMessageEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string EventType { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }
    public string? Error { get; set; }
}
