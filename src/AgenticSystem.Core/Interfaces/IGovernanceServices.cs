using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Interfaces;

/// <summary>
/// #37 — Semantic cache for LLM and tool responses.
/// </summary>
public interface ISemanticCache
{
    Task<SemanticCacheEntry?> GetAsync(string query, string? agentName = null, CancellationToken ct = default);
    Task SetAsync(SemanticCacheEntry entry, CancellationToken ct = default);
    Task InvalidateAsync(string? agentName = null, string? toolName = null, CancellationToken ct = default);
    Task<CacheStats> GetStatsAsync(CancellationToken ct = default);
}

/// <summary>
/// #38 — Compliance and data retention management.
/// </summary>
public interface IComplianceService
{
    Task<RetentionPolicy> SetRetentionPolicyAsync(RetentionPolicy policy, CancellationToken ct = default);
    Task<RetentionPolicy?> GetRetentionPolicyAsync(string? tenantId = null, CancellationToken ct = default);
    Task<DataSubjectRequest> SubmitDataRequestAsync(DataSubjectRequest request, CancellationToken ct = default);
    Task<DataSubjectRequest> ProcessDataRequestAsync(string requestId, CancellationToken ct = default);
    Task<int> EnforceRetentionAsync(CancellationToken ct = default);
}

/// <summary>
/// #40 — Citation engine for grounding responses in source material.
/// </summary>
public interface ICitationEngine
{
    Task<CitedResponse> GenerateWithCitationsAsync(string response, IReadOnlyList<RankedChunk> sourceChunks, CancellationToken ct = default);
    Task<IReadOnlyList<Citation>> ExtractCitationsAsync(string responseText, CancellationToken ct = default);
}

/// <summary>
/// #41 — Knowledge quality governance.
/// </summary>
public interface IKnowledgeGovernance
{
    Task<KnowledgeQualityAssessment> AssessQualityAsync(string documentId, CancellationToken ct = default);
    Task<IReadOnlyList<KnowledgeQualityAssessment>> GetStaleDocumentsAsync(int limit = 50, CancellationToken ct = default);
    Task ScheduleReviewAsync(string documentId, DateTime reviewAt, CancellationToken ct = default);
}
