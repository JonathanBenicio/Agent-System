using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Interfaces;

/// <summary>
/// Estratégia de chunking — divide ParsedDocument em chunks indexáveis.
/// </summary>
public interface IChunkingStrategy
{
    ChunkingStrategyType StrategyType { get; }
    Task<IReadOnlyList<DocumentChunk>> ChunkAsync(ParsedDocument document, ChunkingConfig config, CancellationToken ct = default);
}
