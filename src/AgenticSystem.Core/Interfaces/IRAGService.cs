using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Interfaces;

/// <summary>
/// RAG Service — retrieve → rerank → build context para prompt injection.
/// </summary>
public interface IRAGService
{
    Task<RAGContext> RetrieveContextAsync(RAGQuery query, CancellationToken ct = default);
}
