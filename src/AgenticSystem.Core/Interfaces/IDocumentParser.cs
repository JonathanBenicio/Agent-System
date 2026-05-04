using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Interfaces;

/// <summary>
/// Parser de documentos — cada implementação suporta um tipo específico.
/// </summary>
public interface IDocumentParser
{
    DocumentType SupportedType { get; }
    Task<ParsedDocument> ParseAsync(RawDocument document, CancellationToken ct = default);
}
