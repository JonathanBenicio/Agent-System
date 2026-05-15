namespace AgenticSystem.Core.Models;

// ═══════════════════════════════════════════════════════════
// 🧩 MATURITY LEVEL 1 — Chunk Lifecycle
// ═══════════════════════════════════════════════════════════

/// <summary>
/// Estado do ciclo de vida de um chunk.
/// Novo → Ativo → Consolidado → Arquivado
/// </summary>
public enum ChunkLifecycleState
{
    New,
    Active,
    Consolidated,
    Archived
}

/// <summary>
/// Metadados de lifecycle de um chunk — aging, decay, promotion.
/// </summary>
public class ChunkLifecycle
{
    public string ChunkId { get; set; } = string.Empty;
    public ChunkLifecycleState State { get; set; } = ChunkLifecycleState.New;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastAccessedAt { get; set; } = DateTime.UtcNow;
    public int AccessCount { get; set; }
    public double FreshnessScore { get; set; } = 1.0;
    public double DecayRate { get; set; } = 0.01;
    public string? ConsolidatedIntoId { get; set; }
    public DateTime? ArchivedAt { get; set; }
}

// ═══════════════════════════════════════════════════════════
// 🧩 MATURITY LEVEL 2 — Context Budget
// ═══════════════════════════════════════════════════════════

/// <summary>
/// Orçamento semântico de contexto para um request.
/// </summary>
public class ContextBudget
{
    public int MaxTokens { get; set; } = 4000;
    public double RecentMemoryWeight { get; set; } = 0.4;
    public double DomainKnowledgeWeight { get; set; } = 0.3;
    public double EpisodicWeight { get; set; } = 0.2;
    public double DecisionHistoryWeight { get; set; } = 0.1;
    public ContextStrategy Strategy { get; set; } = ContextStrategy.Balanced;
}

public enum ContextStrategy
{
    Balanced,
    PrecisionFocused,
    CreativityFocused,
    RecencyFocused,
    Minimal
}

/// <summary>
/// Resultado da alocação de budget.
/// </summary>
public class ContextAllocation
{
    public int TotalTokensBudget { get; set; }
    public int TokensUsed { get; set; }
    public int RecentMemoryTokens { get; set; }
    public int DomainKnowledgeTokens { get; set; }
    public int EpisodicTokens { get; set; }
    public int DecisionHistoryTokens { get; set; }
    public ContextStrategy StrategyUsed { get; set; }
    public int ChunksIncluded { get; set; }
    public int ChunksExcluded { get; set; }
    public double EstimatedCost { get; set; }
}

// ═══════════════════════════════════════════════════════════
// 🧩 MATURITY LEVEL 3 — Long-Horizon Tasks
// ═══════════════════════════════════════════════════════════

/// <summary>
/// Plano de execução persistente para tarefas multi-step.
/// </summary>
public class TaskPlan
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<TaskStep> Steps { get; set; } = new();
    public TaskPlanStatus Status { get; set; } = TaskPlanStatus.Created;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public string? SessionId { get; set; }
    public int CurrentStepIndex { get; set; }
    public Dictionary<string, object> Context { get; set; } = new();
}

public class TaskStep
{
    public int Index { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? AssignedAgent { get; set; }
    public TaskStepStatus Status { get; set; } = TaskStepStatus.Pending;
    public string? Result { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public List<string> Dependencies { get; set; } = new();
}

public enum TaskPlanStatus
{
    Created,
    InProgress,
    Paused,
    Completed,
    Failed,
    Cancelled
}

public enum TaskStepStatus
{
    Pending,
    InProgress,
    Completed,
    Failed,
    Skipped
}

// ═══════════════════════════════════════════════════════════
// 🧩 MATURITY LEVEL 4 — Reflection
// ═══════════════════════════════════════════════════════════

/// <summary>
/// Reflexão pós-ação de um agent.
/// </summary>
public class Reflection
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string SessionId { get; set; } = string.Empty;
    public string AgentName { get; set; } = string.Empty;
    public string ActionTaken { get; set; } = string.Empty;
    public string Outcome { get; set; } = string.Empty;
    public double ConfidenceInOutcome { get; set; }
    public List<string> Deviations { get; set; } = new();
    public List<string> LessonsLearned { get; set; } = new();
    public string? ImprovementSuggestion { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ReflectionSeverity Severity { get; set; } = ReflectionSeverity.Info;
}

public enum ReflectionSeverity
{
    Info,
    Warning,
    Critical
}

// ═══════════════════════════════════════════════════════════
// 🧩 MATURITY LEVEL 5 — Human Correction Loop
// ═══════════════════════════════════════════════════════════

/// <summary>
/// Regra de correção persistente criada por humano.
/// </summary>
public class CorrectionRule
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = string.Empty;
    public string Rule { get; set; } = string.Empty;
    public string? Scope { get; set; }
    public string? TargetAgent { get; set; }
    public List<string> Tags { get; set; } = new();
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int TimesApplied { get; set; }
    public DateTime? LastAppliedAt { get; set; }
}

/// <summary>
/// Correção pontual de uma resposta pelo humano.
/// </summary>
public class HumanCorrection
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string SessionId { get; set; } = string.Empty;
    public string OriginalResponse { get; set; } = string.Empty;
    public string CorrectedResponse { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public string? GeneratedRuleId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// ═══════════════════════════════════════════════════════════
// 🧩 MATURITY LEVEL 6 — Knowledge Freshness
// ═══════════════════════════════════════════════════════════

/// <summary>
/// Metadados de frescor de conhecimento.
/// </summary>
public class KnowledgeFreshness
{
    public string DocumentId { get; set; } = string.Empty;
    public double FreshnessScore { get; set; } = 1.0;
    public DateTime ContentDate { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public TimeSpan? ValidityPeriod { get; set; }
    public bool IsPotentiallyStale { get; set; }
    public string? DriftReason { get; set; }
    public DateTime LastVerifiedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Resultado de drift detection em um documento.
/// </summary>
public class DriftReport
{
    public string DocumentId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public bool HasDrift { get; set; }
    public double DriftScore { get; set; }
    public List<string> DriftIndicators { get; set; } = new();
    public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;
}

// ═══════════════════════════════════════════════════════════
// 🧩 MATURITY LEVEL 7 — Confidence Score UX
// ═══════════════════════════════════════════════════════════

/// <summary>
/// Score de confiança exposto ao usuário.
/// </summary>
public class ConfidenceScore
{
    public double Value { get; set; }
    public ConfidenceLevel Level { get; set; }
    public string Label { get; set; } = string.Empty;
    public List<string> Factors { get; set; } = new();
    public bool RequiresConfirmation { get; set; }
}

public enum ConfidenceLevel
{
    High,
    Medium,
    Low,
    RequiresHumanReview
}

// ═══════════════════════════════════════════════════════════
// 🧩 MATURITY LEVEL 8 — Semantic Compression
// ═══════════════════════════════════════════════════════════

/// <summary>
/// Resultado de compressão semântica — transforma histórico em insight.
/// </summary>
public class SemanticSummary
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string SourceType { get; set; } = string.Empty;
    public List<string> SourceIds { get; set; } = new();
    public string CompressedKnowledge { get; set; } = string.Empty;
    public List<string> KeyPrinciples { get; set; } = new();
    public List<string> ActionableInsights { get; set; } = new();
    public int OriginalTokenCount { get; set; }
    public int CompressedTokenCount { get; set; }
    public double CompressionRatio { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? ObsidianNotePath { get; set; }
}

// ═══════════════════════════════════════════════════════════
// 🧩 MATURITY LEVEL 9 — Query Compression (Pre-Retrieval)
// ═══════════════════════════════════════════════════════════

/// <summary>
/// Estratégia de compressão da query antes do retrieval.
/// </summary>
public enum QueryCompressionStrategy
{
    None,
    RemoveRedundancy,
    ExtractKeyTerms,
    SemanticNormalization,
    HybridCompression
}

/// <summary>
/// Resultado da compressão semântica da query antes do retrieval.
/// Remove redundância, normaliza intenção e extrai termos-chave.
/// </summary>
public class CompressedQuery
{
    public string OriginalQuery { get; set; } = string.Empty;
    public string CompressedText { get; set; } = string.Empty;
    public List<string> ExtractedKeyTerms { get; set; } = new();
    public List<string> RemovedRedundancies { get; set; } = new();
    public string NormalizedIntent { get; set; } = string.Empty;
    public QueryCompressionStrategy StrategyUsed { get; set; }
    public int OriginalTokenCount { get; set; }
    public int CompressedTokenCount { get; set; }
    public double CompressionRatio { get; set; }
    public double ConfidenceScore { get; set; }
    public DateTime CompressedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Métricas de qualidade da compressão de query.
/// </summary>
public class QueryCompressionMetrics
{
    public int TotalQueriesCompressed { get; set; }
    public double AverageCompressionRatio { get; set; }
    public double AverageConfidenceScore { get; set; }
    public int RedundanciesRemoved { get; set; }
    public Dictionary<QueryCompressionStrategy, int> StrategyUsage { get; set; } = new();
}

// ═══════════════════════════════════════════════════════════
// 🧩 MATURITY LEVEL 10 — User Personalization Engine
// ═══════════════════════════════════════════════════════════

/// <summary>
/// Nível de tolerância a risco do usuário.
/// </summary>
public enum RiskTolerance
{
    Conservative,
    Moderate,
    Aggressive
}

/// <summary>
/// Estilo de comunicação preferido pelo usuário.
/// </summary>
public enum CommunicationStyle
{
    Concise,
    Detailed,
    Technical,
    Conversational,
    Formal
}

/// <summary>
/// Preferências de resposta do usuário.
/// </summary>
public class ResponsePreferences
{
    public CommunicationStyle Style { get; set; } = CommunicationStyle.Technical;
    public bool IncludeCodeExamples { get; set; } = true;
    public bool IncludeExplanations { get; set; } = true;
    public bool IncludeAlternatives { get; set; }
    public string PreferredLanguage { get; set; } = "pt-br";
    public int MaxResponseTokens { get; set; } = 2000;
}

/// <summary>
/// Perfil completo de personalização do usuário.
/// Modela preferências, estilo, tolerância a risco e histórico de interação.
/// </summary>
public class UserPreferenceProfile
{
    public string UserId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public RiskTolerance RiskTolerance { get; set; } = RiskTolerance.Moderate;
    public ResponsePreferences ResponsePreferences { get; set; } = new();
    public List<string> PreferredAgents { get; set; } = new();
    public List<string> PreferredTools { get; set; } = new();
    public Dictionary<string, string> DomainExpertise { get; set; } = new();
    public Dictionary<string, double> AgentSatisfactionScores { get; set; } = new();
    public int TotalInteractions { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastActiveAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Ajuste de personalização aplicado a uma resposta.
/// </summary>
public class PersonalizationAdjustment
{
    public string UserId { get; set; } = string.Empty;
    public string OriginalPrompt { get; set; } = string.Empty;
    public string AdjustedPrompt { get; set; } = string.Empty;
    public List<string> AppliedPreferences { get; set; } = new();
    public string? RecommendedAgent { get; set; }
    public RiskTolerance AppliedRiskLevel { get; set; }
    public DateTime AppliedAt { get; set; } = DateTime.UtcNow;
}

// ═══════════════════════════════════════════════════════════
// 🧩 MATURITY LEVEL 21 — Scheduled Tasks & Trigger Engine
// ═══════════════════════════════════════════════════════════

/// <summary>
/// Tipo da fonte de dados que o trigger consulta.
/// </summary>
public enum TriggerSourceType
{
    HttpGet,
    HttpPost,
    HealthCheck,
    Metric
}

/// <summary>
/// Tipo de condição avaliada pelo trigger engine.
/// </summary>
public enum ConditionType
{
    StatusCode,
    JsonPath,
    Threshold,
    Contains,
    Regex,
    MlClassification
}

/// <summary>
/// Status de uma tarefa agendada.
/// </summary>
public enum ScheduledTaskStatus
{
    Active,
    Paused,
    Completed,
    Failed,
    Disabled
}

/// <summary>
/// Resultado da entrega de notificação.
/// </summary>
public enum DeliveryStatus
{
    Success,
    Failed,
    Retrying,
    Skipped
}

/// <summary>
/// Fonte de dados que o trigger consulta para avaliação.
/// </summary>
public record TriggerSource(
    TriggerSourceType Type,
    string Endpoint,
    Dictionary<string, string> Headers,
    string? Body = null);

/// <summary>
/// Condição que determina se o trigger deve disparar.
/// </summary>
public record TriggerCondition(
    ConditionType Type,
    string Expression,
    string? ExpectedValue = null);

/// <summary>
/// Ação executada quando a condição do trigger é satisfeita.
/// </summary>
public record TriggerAction(
    string ActionType,
    string Description,
    Dictionary<string, string> Parameters);

/// <summary>
/// Regra de trigger completa: fonte + condição + ação + agendamento.
/// </summary>
public class TriggerRule
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Schedule { get; set; } = string.Empty; // CRON expression ou TimeSpan
    public TriggerSource Source { get; set; } = null!;
    public TriggerCondition Condition { get; set; } = null!;
    public TriggerAction Action { get; set; } = null!;
    public string[] DeliveryChannels { get; set; } = ["webhook"];
    public bool Enabled { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastTriggeredAt { get; set; }
    public int ExecutionCount { get; set; }
    public string? TimeZoneId { get; set; }
}

/// <summary>
/// Tarefa agendada registrada no sistema.
/// </summary>
public class ScheduledTask
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Schedule { get; set; } = string.Empty; // CRON ou intervalo
    public TimeSpan? Interval { get; set; }
    public ScheduledTaskStatus Status { get; set; } = ScheduledTaskStatus.Active;
    public int MaxRetryAttempts { get; set; } = 3;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? NextRunAt { get; set; }
    public DateTime? LastRunAt { get; set; }
    public DateTime? LastFailedAt { get; set; }
    public int TotalExecutions { get; set; }
    public int FailedExecutions { get; set; }
    public int ConsecutiveFailures { get; set; }
    public string? DeadLetterReason { get; set; }
    public TriggerRule? AssociatedRule { get; set; }
    public string? TimeZoneId { get; set; }
    public List<string> DependencyTaskIds { get; set; } = new();
    public List<string> ContinuationTaskIds { get; set; } = new();
}

/// <summary>
/// Registro de uma execução individual de tarefa agendada.
/// </summary>
public class TaskExecution
{
    public string ExecutionId { get; set; } = Guid.NewGuid().ToString();
    public string TaskId { get; set; } = string.Empty;
    public int AttemptNumber { get; set; } = 1;
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public bool Success { get; set; }
    public bool DeadLettered { get; set; }
    public string? ErrorMessage { get; set; }
    public TimeSpan Duration => CompletedAt.HasValue ? CompletedAt.Value - StartedAt : TimeSpan.Zero;
}

/// <summary>
/// Resultado de avaliação de um trigger.
/// </summary>
public class TriggerEvaluationResult
{
    public string RuleId { get; set; } = string.Empty;
    public string RuleName { get; set; } = string.Empty;
    public bool ConditionMet { get; set; }
    public string? ActualValue { get; set; }
    public string? ExpectedValue { get; set; }
    public DateTime EvaluatedAt { get; set; } = DateTime.UtcNow;
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Resultado de entrega de notificação via canal.
/// </summary>
public class DeliveryResult
{
    public string ChannelName { get; set; } = string.Empty;
    public DeliveryStatus Status { get; set; }
    public int Attempts { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public string? ErrorMessage { get; set; }
    public int HttpStatusCode { get; set; }
}

/// <summary>
/// Payload enviado nos canais de notificação.
/// </summary>
public class TriggerNotificationPayload
{
    public string TriggerName { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string ConditionResult { get; set; } = string.Empty;
    public string? SuggestedAction { get; set; }
    public string? ActualValue { get; set; }
    public string? ExpectedValue { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
}

// ═══════════════════════════════════════════════════════════════
// ML22 — Credential & Configuration Management
// ═══════════════════════════════════════════════════════════════

/// <summary>
/// Categoria de configuração gerenciada.
/// </summary>
public enum ConfigCategory
{
    Credentials,
    Paths,
    Connection,
    Provider,
    General
}

/// <summary>
/// Status de uma entrada de configuração.
/// </summary>
public enum ConfigEntryStatus
{
    Active,
    Disabled,
    Expired,
    PendingRotation
}

/// <summary>
/// Entrada de configuração armazenada (valores sensíveis encriptados).
/// </summary>
public class ConfigEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string? EncryptedValue { get; set; }
    public bool IsSecret { get; set; }
    public ConfigCategory Category { get; set; } = ConfigCategory.General;
    public ConfigEntryStatus Status { get; set; } = ConfigEntryStatus.Active;
    public string? Description { get; set; }
    public string? Provider { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
}

/// <summary>
/// Request para criar/atualizar uma configuração.
/// </summary>
public class ConfigEntryRequest
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public bool IsSecret { get; set; }
    public ConfigCategory Category { get; set; } = ConfigCategory.General;
    public string? Description { get; set; }
    public string? Provider { get; set; }
    public DateTime? ExpiresAt { get; set; }
}

/// <summary>
/// Resultado de validação de config.
/// </summary>
public class ConfigValidationResult
{
    public bool IsValid { get; set; }
    public string Key { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public DateTime ValidatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Histórico de mudanças de configuração (audit trail).
/// </summary>
public class ConfigChangeLog
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ConfigKey { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty; // Created, Updated, Deleted, Rotated
    public string? ChangedBy { get; set; }
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
    public string? PreviousValueHash { get; set; }
    public string? NewValueHash { get; set; }
}

// ═══════════════════════════════════════════════════════════════
// ML23 — Embedding Migration & Re-indexing
// ═══════════════════════════════════════════════════════════════

/// <summary>
/// Status de uma migração de embedding.
/// </summary>
public enum MigrationStatus
{
    Pending,
    InProgress,
    Completed,
    Failed,
    Cancelled,
    RollingBack
}

/// <summary>
/// Provedor de embedding suportado.
/// </summary>
public enum EmbeddingProvider
{
    OpenAI,
    Google,
    Ollama,
    Cohere,
    Onnx,
    Custom
}

/// <summary>
/// Configuração de modelo de embedding.
/// </summary>
public class EmbeddingModelConfig
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public EmbeddingProvider Provider { get; set; }
    public string ModelName { get; set; } = string.Empty;
    public int Dimensions { get; set; }
    public string? ApiKey { get; set; }
    public string? BaseUrl { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Job de migração de embedding.
/// </summary>
public class EmbeddingMigrationJob
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string SourceCollection { get; set; } = string.Empty;
    public string TargetCollection { get; set; } = string.Empty;
    public EmbeddingModelConfig SourceModel { get; set; } = new();
    public EmbeddingModelConfig TargetModel { get; set; } = new();
    public MigrationStatus Status { get; set; } = MigrationStatus.Pending;
    public int TotalDocuments { get; set; }
    public int ProcessedDocuments { get; set; }
    public int FailedDocuments { get; set; }
    public double ProgressPercentage => TotalDocuments > 0 ? (double)ProcessedDocuments / TotalDocuments * 100 : 0;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }
    public List<string> ProcessingLog { get; set; } = new();
}

/// <summary>
/// Request para iniciar migração.
/// </summary>
public class StartMigrationRequest
{
    public string SourceModelId { get; set; } = string.Empty;
    public string TargetModelId { get; set; } = string.Empty;
    public string? SourceCollection { get; set; }
    public bool AutoSwitch { get; set; } = true;
    public bool DeleteSourceAfterCompletion { get; set; } = false;
}

/// <summary>
/// Status resumido de migração para o frontend.
/// </summary>
public class MigrationStatusSummary
{
    public string JobId { get; set; } = string.Empty;
    public MigrationStatus Status { get; set; }
    public double ProgressPercentage { get; set; }
    public int TotalDocuments { get; set; }
    public int ProcessedDocuments { get; set; }
    public int FailedDocuments { get; set; }
    public string SourceModel { get; set; } = string.Empty;
    public string TargetModel { get; set; } = string.Empty;
    public TimeSpan? ElapsedTime { get; set; }
    public TimeSpan? EstimatedTimeRemaining { get; set; }
}
