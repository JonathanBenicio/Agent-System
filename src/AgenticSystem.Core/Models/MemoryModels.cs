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
    public string Content { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // note, agent, decision, domain
    public string Collection { get; set; } = string.Empty;
    public float[] Embedding { get; set; } = Array.Empty<float>();
    public Dictionary<string, string> Metadata { get; set; } = new();
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
    public DateTime IndexedAt { get; set; }
}