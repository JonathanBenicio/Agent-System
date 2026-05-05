namespace AgenticSystem.Core.Models;

/// <summary>
/// Query para o RAG pipeline
/// </summary>
public class RAGQuery
{
    public string Query { get; set; } = string.Empty;
    public string? AgentId { get; set; }
    public string? SessionId { get; set; }
    public SearchScope Scope { get; set; } = SearchScope.All;
    public int MaxResults { get; set; } = 10;
    public int TopKAfterReRank { get; set; } = 5;
    public double MinRelevanceScore { get; set; } = 0.3;
    public Dictionary<string, string>? Filters { get; set; }
    public RetrievalStrategy Strategy { get; set; } = RetrievalStrategy.Default;
}

/// <summary>
/// Estratégia de retrieval — o agent decide qual contexto buscar
/// </summary>
public enum RetrievalStrategy
{
    Default,          // Busca geral
    RecentMemory,     // Foco em memória recente (últimas sessões)
    DomainKnowledge,  // Foco em documentos de domínio
    DecisionHistory,  // Foco em decisões passadas
    Episodic,         // Foco em episódios/sessões específicos
    Targeted          // Busca direcionada com filtros explícitos
}

/// <summary>
/// Contexto montado pelo RAG pipeline para injeção no prompt
/// </summary>
public class RAGContext
{
    public string Query { get; set; } = string.Empty;
    public string EffectiveQuery { get; set; } = string.Empty;
    public List<string> QueryVariants { get; set; } = new();
    public bool UsedHydeExpansion { get; set; }
    public string? HydeVariant { get; set; }
    public List<RankedChunk> Chunks { get; set; } = new();
    public string BuiltContext { get; set; } = string.Empty;
    public string? SemanticSummary { get; set; }
    public bool UsedSemanticCompression { get; set; }
    public int OriginalContextTokens { get; set; }
    public int TotalTokensUsed { get; set; }
    public int CandidatesRetrieved { get; set; }
    public int CandidatesAfterReRank { get; set; }
    public RetrievalStrategy StrategyUsed { get; set; }
    public TimeSpan RetrievalTime { get; set; }
    public TimeSpan ReRankTime { get; set; }
    public TimeSpan TotalTime { get; set; }
}

/// <summary>
/// Chunk com score de re-ranking
/// </summary>
public class RankedChunk
{
    public string Id { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public double OriginalScore { get; set; }
    public double ReRankedScore { get; set; }
    public int Rank { get; set; }
    public string Source { get; set; } = string.Empty;
    public string Section { get; set; } = string.Empty;
    public Dictionary<string, string> Metadata { get; set; } = new();
}
