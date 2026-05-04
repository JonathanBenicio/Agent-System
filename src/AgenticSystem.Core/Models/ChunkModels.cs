namespace AgenticSystem.Core.Models;

/// <summary>
/// Chunk de documento com metadados ricos para RAG
/// </summary>
public class DocumentChunk
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string DocumentId { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public int Index { get; set; }
    public int TokenCount { get; set; }
    public ChunkMetadata Metadata { get; set; } = new();
    public float[] Embedding { get; set; } = Array.Empty<float>();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Converte chunk para EmbeddingDocument para indexação no VectorStore
    /// </summary>
    public EmbeddingDocument ToEmbeddingDocument()
    {
        return new EmbeddingDocument
        {
            Id = Id,
            Content = Content,
            Type = Metadata.ContentType,
            Collection = Metadata.Collection,
            Embedding = Embedding,
            Metadata = Metadata.ToDictionary(),
            IndexedAt = CreatedAt
        };
    }
}

/// <summary>
/// Metadados ricos do chunk — essenciais para filtragem e agentic retrieval
/// </summary>
public class ChunkMetadata
{
    public string Source { get; set; } = string.Empty;       // obsidian, upload, email
    public string FileName { get; set; } = string.Empty;     // "Projeto X.md"
    public string Section { get; set; } = string.Empty;      // "Decisões Técnicas"
    public int SectionLevel { get; set; }                     // 1, 2, 3
    public string ContentType { get; set; } = string.Empty;  // decision, note, domain, runbook
    public string Collection { get; set; } = "default";      // coleção no VectorStore
    public string DocumentHash { get; set; } = string.Empty;
    public int ChunkIndex { get; set; }
    public int TotalChunks { get; set; }
    public bool HasOverlap { get; set; }
    public string? AgentId { get; set; }
    public List<string> Tags { get; set; } = new();
    public DateTime DocumentDate { get; set; }

    public Dictionary<string, string> ToDictionary()
    {
        var dict = new Dictionary<string, string>
        {
            ["source"] = Source,
            ["file_name"] = FileName,
            ["section"] = Section,
            ["section_level"] = SectionLevel.ToString(),
            ["content_type"] = ContentType,
            ["collection"] = Collection,
            ["document_hash"] = DocumentHash,
            ["chunk_index"] = ChunkIndex.ToString(),
            ["total_chunks"] = TotalChunks.ToString(),
            ["has_overlap"] = HasOverlap.ToString(),
            ["document_date"] = DocumentDate.ToString("O")
        };

        if (!string.IsNullOrEmpty(AgentId))
            dict["agent_id"] = AgentId;

        if (Tags.Count > 0)
            dict["tags"] = string.Join(",", Tags);

        return dict;
    }
}

/// <summary>
/// Configuração de chunking
/// </summary>
public class ChunkingConfig
{
    /// <summary>Tamanho alvo do chunk em tokens (default: 500)</summary>
    public int TargetTokens { get; set; } = 500;

    /// <summary>Tamanho máximo do chunk em tokens (default: 1000)</summary>
    public int MaxTokens { get; set; } = 1000;

    /// <summary>Tamanho mínimo do chunk em tokens — evita chunks inúteis (default: 50)</summary>
    public int MinTokens { get; set; } = 50;

    /// <summary>Percentual de overlap entre chunks adjacentes (0.0-0.5, default: 0.15 = 15%)</summary>
    public double OverlapPercent { get; set; } = 0.15;

    /// <summary>Coleção destino no VectorStore</summary>
    public string Collection { get; set; } = "default";

    /// <summary>Tipo de conteúdo para metadados</summary>
    public string ContentType { get; set; } = "document";
}

/// <summary>
/// Estratégia de chunking
/// </summary>
public enum ChunkingStrategyType
{
    FixedSize,
    Structural,
    Hybrid
}

/// <summary>
/// Resultado da ingestão de um documento
/// </summary>
public class IngestionResult
{
    public string DocumentId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public bool Success { get; set; }
    public int ChunksCreated { get; set; }
    public int TokensProcessed { get; set; }
    public string ContentHash { get; set; } = string.Empty;
    public string? Error { get; set; }
    public TimeSpan Duration { get; set; }

    public static IngestionResult Ok(string docId, string fileName, int chunks, int tokens, string hash, TimeSpan duration)
        => new()
        {
            DocumentId = docId,
            FileName = fileName,
            Success = true,
            ChunksCreated = chunks,
            TokensProcessed = tokens,
            ContentHash = hash,
            Duration = duration
        };

    public static IngestionResult Fail(string docId, string fileName, string error)
        => new()
        {
            DocumentId = docId,
            FileName = fileName,
            Success = false,
            Error = error
        };
}
