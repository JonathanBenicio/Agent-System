using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class DocumentController : ControllerBase
{
    private readonly IDocumentIngestionPipeline _ingestionPipeline;
    private readonly ILogger<DocumentController> _logger;

    public DocumentController(
        IDocumentIngestionPipeline ingestionPipeline,
        ILogger<DocumentController> logger)
    {
        _ingestionPipeline = ingestionPipeline;
        _logger = logger;
    }

    /// <summary>
    /// Ingere um documento no pipeline RAG (parse → chunk → embed → index).
    /// </summary>
    [HttpPost("ingest")]
    public async Task<IActionResult> IngestDocument(
        IFormFile file,
        [FromQuery] string? source = null,
        CancellationToken ct = default)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { error = "Arquivo não fornecido ou vazio." });

        var documentType = ResolveDocumentType(file.FileName);
        if (documentType == null)
            return BadRequest(new { error = $"Tipo de arquivo não suportado: {Path.GetExtension(file.FileName)}" });

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);

        var rawDocument = new RawDocument
        {
            FileName = file.FileName,
            Type = documentType.Value,
            Content = ms.ToArray(),
            Source = source ?? "upload",
            Metadata = new Dictionary<string, string>
            {
                ["contentType"] = file.ContentType,
                ["size"] = file.Length.ToString()
            }
        };

        _logger.LogInformation("📄 Ingestão iniciada: {FileName} ({Size} bytes, type: {Type})",
            file.FileName, file.Length, documentType);

        var result = await _ingestionPipeline.IngestAsync(rawDocument, config: null, ct);

        if (!result.Success)
        {
            _logger.LogWarning("❌ Ingestão falhou: {FileName} — {Error}", file.FileName, result.Error);
            return UnprocessableEntity(new { error = result.Error, documentId = result.DocumentId });
        }

        _logger.LogInformation("✅ Ingestão concluída: {FileName} → {Chunks} chunks, {Tokens} tokens em {Duration}ms",
            file.FileName, result.ChunksCreated, result.TokensProcessed, result.Duration.TotalMilliseconds);

        return Ok(new
        {
            result.DocumentId,
            result.FileName,
            result.ChunksCreated,
            result.TokensProcessed,
            result.ContentHash,
            DurationMs = result.Duration.TotalMilliseconds
        });
    }

    /// <summary>
    /// Ingere múltiplos documentos em batch.
    /// </summary>
    [HttpPost("ingest/batch")]
    public async Task<IActionResult> IngestBatch(
        [FromForm] IFormFileCollection files,
        [FromQuery] string? source = null,
        CancellationToken ct = default)
    {
        if (files == null || files.Count == 0)
            return BadRequest(new { error = "Nenhum arquivo fornecido." });

        var rawDocuments = new List<RawDocument>();

        foreach (var file in files)
        {
            var documentType = ResolveDocumentType(file.FileName);
            if (documentType == null)
            {
                _logger.LogWarning("⚠️ Arquivo ignorado (tipo não suportado): {FileName}", file.FileName);
                continue;
            }

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms, ct);

            rawDocuments.Add(new RawDocument
            {
                FileName = file.FileName,
                Type = documentType.Value,
                Content = ms.ToArray(),
                Source = source ?? "upload",
                Metadata = new Dictionary<string, string>
                {
                    ["contentType"] = file.ContentType,
                    ["size"] = file.Length.ToString()
                }
            });
        }

        if (rawDocuments.Count == 0)
            return BadRequest(new { error = "Nenhum arquivo com tipo suportado." });

        _logger.LogInformation("📄 Batch ingestão: {Count} documentos", rawDocuments.Count);

        var results = await _ingestionPipeline.IngestBatchAsync(rawDocuments, config: null, ct);

        return Ok(new
        {
            total = results.Count,
            succeeded = results.Count(r => r.Success),
            failed = results.Count(r => !r.Success),
            results = results.Select(r => new
            {
                r.DocumentId,
                r.FileName,
                r.Success,
                r.ChunksCreated,
                r.TokensProcessed,
                r.Error,
                DurationMs = r.Duration.TotalMilliseconds
            })
        });
    }

    private static DocumentType? ResolveDocumentType(string fileName)
    {
        var ext = Path.GetExtension(fileName)?.ToLowerInvariant();
        return ext switch
        {
            ".md" => DocumentType.Markdown,
            ".txt" => DocumentType.PlainText,
            ".pdf" => DocumentType.Pdf,
            ".docx" => DocumentType.Docx,
            ".html" or ".htm" => DocumentType.Html,
            ".pptx" => DocumentType.Pptx,
            ".png" or ".jpg" or ".jpeg" or ".gif" or ".webp" => DocumentType.Image,
            _ => null
        };
    }
}
