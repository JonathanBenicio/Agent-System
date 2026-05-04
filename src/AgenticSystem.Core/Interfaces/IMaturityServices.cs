using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Interfaces;

// ═══════════════════════════════════════════════════════════
// Maturity Level 1 — Chunk Lifecycle Management
// ═══════════════════════════════════════════════════════════

/// <summary>
/// Gerencia ciclo de vida de chunks: aging, decay, promotion e consolidação.
/// Fluxo: Novo → Ativo → Consolidado → Arquivado.
/// </summary>
public interface IChunkLifecycleManager
{
    Task<ChunkLifecycle> GetLifecycleAsync(string chunkId);
    Task PromoteAsync(string chunkId);
    Task ArchiveAsync(string chunkId);
    Task<IEnumerable<ChunkLifecycle>> GetStaleChunksAsync(TimeSpan threshold);
    Task ConsolidateChunksAsync(IEnumerable<string> chunkIds, string targetChunkId);
    Task ApplyDecayAsync();
    Task RecordAccessAsync(string chunkId);
}

// ═══════════════════════════════════════════════════════════
// Maturity Level 2 — Context Budget Manager
// ═══════════════════════════════════════════════════════════

/// <summary>
/// Controla orçamento semântico de contexto por request.
/// Decide quanto contexto usar, de que tipo, e o que excluir.
/// </summary>
public interface IContextBudgetManager
{
    ContextBudget ResolveBudget(AnalysisResult analysis);
    Task<ContextAllocation> AllocateContextAsync(ContextBudget budget, RAGContext ragContext);
    Task<RAGContext> TrimContextToBudgetAsync(RAGContext ragContext, ContextBudget budget);
}

// ═══════════════════════════════════════════════════════════
// Maturity Level 3 — Long-Horizon Task Planning
// ═══════════════════════════════════════════════════════════

/// <summary>
/// Gerencia decomposição e execução persistente de tarefas multi-step.
/// </summary>
public interface ITaskPlanManager
{
    Task<TaskPlan> CreatePlanAsync(string userId, string objective, List<TaskStep> steps);
    Task<TaskPlan?> GetPlanAsync(string planId);
    Task<IEnumerable<TaskPlan>> GetActivePlansAsync(string userId);
    Task AdvanceStepAsync(string planId, string? result = null);
    Task FailStepAsync(string planId, string reason);
    Task PausePlanAsync(string planId);
    Task ResumePlanAsync(string planId);
    Task CancelPlanAsync(string planId);
}

// ═══════════════════════════════════════════════════════════
// Maturity Level 4 — Reflection Agent
// ═══════════════════════════════════════════════════════════

/// <summary>
/// Reflexão pós-ação: avalia resultado, confiança, desvios e gera learnings.
/// </summary>
public interface IReflectionEngine
{
    Task<Reflection> ReflectAsync(string sessionId, string agentName, string action, string outcome, double confidence);
    Task<IEnumerable<Reflection>> GetSessionReflectionsAsync(string sessionId);
    Task<IEnumerable<Reflection>> GetRecentLearningsAsync(int count = 10);
}

// ═══════════════════════════════════════════════════════════
// Maturity Level 5 — Human Correction Loop
// ═══════════════════════════════════════════════════════════

/// <summary>
/// Gerencia correções humanas e regras persistentes derivadas.
/// </summary>
public interface ICorrectionLoop
{
    Task<CorrectionRule> AddRuleAsync(string userId, string rule, string? scope = null, string? targetAgent = null);
    Task<IEnumerable<CorrectionRule>> GetActiveRulesAsync(string userId, string? agentName = null);
    Task<HumanCorrection> RecordCorrectionAsync(string sessionId, string original, string corrected, string? reason = null);
    Task DeactivateRuleAsync(string ruleId);
    Task<string> ApplyRulesToPromptAsync(string prompt, IEnumerable<CorrectionRule> rules);
}

// ═══════════════════════════════════════════════════════════
// Maturity Level 6 — Knowledge Freshness & Drift Detection
// ═══════════════════════════════════════════════════════════

/// <summary>
/// Monitora frescor de documentos e detecta drift de conhecimento.
/// </summary>
public interface IKnowledgeFreshnessService
{
    Task<KnowledgeFreshness> GetFreshnessAsync(string documentId);
    Task SetValidityPeriodAsync(string documentId, TimeSpan validity);
    Task<IEnumerable<KnowledgeFreshness>> GetStaleDocumentsAsync();
    Task<DriftReport> DetectDriftAsync(string documentId);
    Task MarkVerifiedAsync(string documentId);
    Task<double> CalculateFreshnessScoreAsync(string documentId);
}

// ═══════════════════════════════════════════════════════════
// Maturity Level 7 — Confidence Score UX
// ═══════════════════════════════════════════════════════════

/// <summary>
/// Calcula e expõe score de confiança ao usuário.
/// </summary>
public interface IConfidenceScoreCalculator
{
    ConfidenceScore Calculate(AgentResponse response, RAGContext? ragContext = null, IEnumerable<Reflection>? reflections = null);
    ConfidenceScore Calculate(AgentResponse response, RAGContext? ragContext, IEnumerable<Reflection>? reflections, ToolAvailabilityResult? toolAvailability);
}

// ═══════════════════════════════════════════════════════════
// Maturity Level 8 — Semantic Compression
// ═══════════════════════════════════════════════════════════

/// <summary>
/// Comprime histórico em princípios reutilizáveis e insights acionáveis.
/// </summary>
public interface ISemanticCompressor
{
    Task<SemanticSummary> CompressSessionAsync(string sessionId);
    Task<SemanticSummary> CompressChunksAsync(IEnumerable<string> chunkIds, string label);
    Task<IEnumerable<SemanticSummary>> GetInsightsAsync(string? sourceType = null, int count = 10);
}

// ═══════════════════════════════════════════════════════════
// Maturity Level 9 — Query Compression (Pre-Retrieval)
// ═══════════════════════════════════════════════════════════

/// <summary>
/// Comprime e normaliza queries antes do retrieval — remove redundância,
/// extrai termos-chave e normaliza intenção para melhorar precisão de busca.
/// </summary>
public interface IQueryCompressor
{
    Task<CompressedQuery> CompressAsync(string query, QueryCompressionStrategy strategy = QueryCompressionStrategy.HybridCompression);
    Task<CompressedQuery> CompressWithContextAsync(string query, AnalysisResult? analysisContext = null);
    QueryCompressionMetrics GetMetrics();
}

// ═══════════════════════════════════════════════════════════
// Maturity Level 10 — User Personalization Engine
// ═══════════════════════════════════════════════════════════

/// <summary>
/// Engine de personalização profunda por usuário — modela preferências,
/// estilo individual, tolerância a risco e aplica ajustes em prompts/respostas.
/// </summary>
public interface IUserPreferenceEngine
{
    Task<UserPreferenceProfile> GetOrCreateProfileAsync(string userId, string? displayName = null);
    Task<UserPreferenceProfile> UpdateProfileAsync(UserPreferenceProfile profile);
    Task<PersonalizationAdjustment> PersonalizePromptAsync(string userId, string prompt);
    Task RecordInteractionAsync(string userId, string agentName, double satisfactionScore);
    Task<string?> RecommendAgentAsync(string userId, AnalysisResult analysis);
    Task DeactivateProfileAsync(string userId);
}
