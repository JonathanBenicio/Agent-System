namespace AgenticSystem.Core.Models;

/// <summary>
/// Nota do Obsidian
/// </summary>
public class ObsidianNote
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public List<string> BackLinks { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Dictionary<string, object> Frontmatter { get; set; } = new();
}

/// <summary>
/// Documento para indexação vetorial
/// </summary>
public class EmbeddingDocument
{
    public string Id { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // note, agent, decision, domain
    public string Collection { get; set; } = string.Empty;
    public float[]? Embedding { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
    public string? ContextualSummary { get; set; }
    public DateTime IndexedAt { get; set; } = DateTime.UtcNow;

    public static EmbeddingDocument FromObsidianNote(ObsidianNote note)
    {
        return new EmbeddingDocument
        {
            Id = note.Id,
            Content = $"{note.Title}\n\n{note.Content}",
            Type = "note",
            Collection = "notes",
            Metadata = new Dictionary<string, string>
            {
                ["title"] = note.Title,
                ["file_path"] = note.FilePath,
                ["tags"] = string.Join(",", note.Tags),
                ["updated_at"] = note.UpdatedAt.ToString("O")
            }
        };
    }
}

public class VectorStoreStats
{
    public string TenantId { get; set; } = string.Empty;
    public long DocumentCount { get; set; }
    public long TotalBytes { get; set; }
}
/// <summary>
/// Resultado de busca vetorial
/// </summary>
public class SearchResult
{
    public List<SearchMatch> Matches { get; set; } = new();
    public int TotalFound { get; set; }
    public string Query { get; set; } = string.Empty;
    public SearchScope Scope { get; set; }
    public TimeSpan ExecutionTime { get; set; }
}

/// <summary>
/// Match individual de busca
/// </summary>
public class SearchMatch
{
    public string Id { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Collection { get; set; } = string.Empty;
    public double Score { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
    public string? Snippet { get; set; }
    public float[]? Embedding { get; set; }
    public string? ContextualSummary { get; set; }
    public DateTime IndexedAt { get; set; }
}

public sealed record VectorDocumentUsage(string Id, string? DocumentId, long? SourceBytes, long ContentBytes, long EmbeddingBytes);

public static class VectorUsageCalculator
{
    public static VectorStoreStats Calculate(string tenantId, IEnumerable<VectorDocumentUsage> documents)
    {
        var groups = documents.GroupBy(document => string.IsNullOrWhiteSpace(document.DocumentId) ? document.Id : document.DocumentId,
            StringComparer.Ordinal);
        long documentCount = 0;
        long totalBytes = 0;

        foreach (var group in groups)
        {
            documentCount++;
            var sourceBytes = group.Max(document => document.SourceBytes ?? 0);
            totalBytes += sourceBytes > 0
                ? sourceBytes
                : group.Sum(document => document.ContentBytes + document.EmbeddingBytes);
        }

        return new VectorStoreStats { TenantId = tenantId, DocumentCount = documentCount, TotalBytes = totalBytes };
    }
}
