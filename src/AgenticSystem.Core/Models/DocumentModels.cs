namespace AgenticSystem.Core.Models;

/// <summary>
/// Tipo de documento para parsing
/// </summary>
public enum DocumentType
{
    Markdown,
    PlainText,
    Pdf,
    Docx,
    Html,
    Pptx,
    Image
}

/// <summary>
/// Documento bruto antes do parsing
/// </summary>
public class RawDocument
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string FileName { get; set; } = string.Empty;
    public DocumentType Type { get; set; }
    public byte[] Content { get; set; } = Array.Empty<byte>();
    public string? TextContent { get; set; }
    public string Source { get; set; } = string.Empty; // obsidian, upload, email, api
    public Dictionary<string, string> Metadata { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Documento após parsing — texto estruturado com seções
/// </summary>
public class ParsedDocument
{
    public string Id { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public DocumentType OriginalType { get; set; }
    public string FullText { get; set; } = string.Empty;
    public List<DocumentSection> Sections { get; set; } = new();
    public string Source { get; set; } = string.Empty;
    public Dictionary<string, string> Metadata { get; set; } = new();
    public string ContentHash { get; set; } = string.Empty;
    public DateTime ParsedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Seção de um documento (título + conteúdo + nível)
/// </summary>
public class DocumentSection
{
    public int Index { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public int Level { get; set; } // 1=H1, 2=H2, etc. 0=body sem header
    public SectionType Type { get; set; }
}

public enum SectionType
{
    Heading,
    Paragraph,
    List,
    Table,
    CodeBlock,
    Frontmatter
}
