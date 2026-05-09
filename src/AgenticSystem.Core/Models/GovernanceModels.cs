namespace AgenticSystem.Core.Models;

// ═══════════════════════════════════════════════════════════
// #37 — Intelligent Caching
// ═══════════════════════════════════════════════════════════

/// <summary>
/// Semantic cache entry for LLM/tool results.
/// </summary>
public class SemanticCacheEntry
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string QueryHash { get; init; } = string.Empty;
    public string QueryText { get; init; } = string.Empty;
    public string ResponseText { get; init; } = string.Empty;
    public double SemanticSimilarityThreshold { get; init; } = 0.95;
    public float[]? QueryEmbedding { get; init; }
    public string? AgentName { get; init; }
    public string? ToolName { get; init; }
    public int HitCount { get; set; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; init; }
    public DateTime LastHitAt { get; set; }
}

public class SemanticCacheResult
{
    public bool IsHit { get; set; }
    public string? CachedResponse { get; set; }
    public double SimilarityScore { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
}

/// <summary>
/// Cache statistics.
/// </summary>
public class CacheStats
{
    public int TotalEntries { get; init; }
    public int HitCount { get; init; }
    public int MissCount { get; init; }
    public double HitRate => HitCount + MissCount > 0 ? (double)HitCount / (HitCount + MissCount) : 0;
    public double EstimatedSavingsUsd { get; init; }
    public int TokensSaved { get; init; }
}

// ═══════════════════════════════════════════════════════════
// #38 — Compliance & Retention
// ═══════════════════════════════════════════════════════════

/// <summary>
/// Data retention policy.
/// </summary>
public class RetentionPolicy
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Name { get; init; } = string.Empty;
    public string? TenantId { get; init; }
    public RetentionScope Scope { get; init; }
    public TimeSpan RetentionPeriod { get; init; } = TimeSpan.FromDays(365);
    public RetentionAction ActionOnExpiry { get; init; } = RetentionAction.Archive;
    public bool AllowManualDeletion { get; init; } = true;
    public bool RequireConsentForRetention { get; init; } = false;
}

public enum RetentionScope
{
    ConversationHistory,
    AuditLogs,
    UserData,
    AgentMemory,
    Documents,
    AllData
}

public enum RetentionAction
{
    Archive,    // Move to cold storage
    Delete,     // Permanently remove
    Anonymize,  // Strip PII, keep structure
    Export      // Export before deletion
}

/// <summary>
/// GDPR/LGPD data subject request.
/// </summary>
public class DataSubjectRequest
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public DataSubjectRequestType RequestType { get; init; }
    public string SubjectId { get; init; } = string.Empty;
    public string? SubjectEmail { get; init; }
    public DataSubjectRequestStatus Status { get; set; } = DataSubjectRequestStatus.Pending;
    public DateTime RequestedAt { get; init; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public string? ExportUrl { get; set; }
}

public enum DataSubjectRequestType
{
    Access,     // Right to access
    Deletion,   // Right to be forgotten
    Export,     // Data portability
    Rectification,
    Restriction
}

public enum DataSubjectRequestStatus
{
    Pending,
    Processing,
    Completed,
    Denied
}

// ═══════════════════════════════════════════════════════════
// #40 — Citation Engine
// ═══════════════════════════════════════════════════════════

/// <summary>
/// A citation linking a response to its source material.
/// </summary>
public class Citation
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string SourceDocumentId { get; init; } = string.Empty;
    public string SourceDocumentName { get; init; } = string.Empty;
    public int? PageNumber { get; init; }
    public string? Section { get; init; }
    public string RelevantExcerpt { get; init; } = string.Empty;
    public double Confidence { get; init; }
    public CitationType Type { get; init; }
}

public enum CitationType
{
    DirectQuote,
    Paraphrase,
    Summary,
    Reference,
    Inference
}

/// <summary>
/// A response with inline citations.
/// </summary>
public class CitedResponse
{
    public string ResponseText { get; init; } = string.Empty;
    public string CitedText { get; init; } = string.Empty;
    public List<Citation> Citations { get; init; } = [];
    public double OverallConfidence { get; init; }
    public int UncitedStatements { get; init; }
}

// ═══════════════════════════════════════════════════════════
// #41 — Knowledge Governance (extended)
// ═══════════════════════════════════════════════════════════

/// <summary>
/// Knowledge quality assessment for a document or chunk.
/// </summary>
public class KnowledgeQualityAssessment
{
    public string DocumentId { get; init; } = string.Empty;
    public double FreshnessScore { get; init; }
    public double AccuracyScore { get; init; }
    public double RelevanceScore { get; init; }
    public double OverallQuality { get; init; }
    public bool IsStale { get; init; }
    public bool NeedsReview { get; init; }
    public DateTime AssessedAt { get; init; } = DateTime.UtcNow;
    public DateTime? NextReviewAt { get; init; }
}
