#if DEBUG || STAGING
using Microsoft.AspNetCore.Mvc;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using System.Diagnostics;

namespace AgenticSystem.Api.Controllers;

[ApiController]
[Route("api/test/rag")]
public class RagTestController : ControllerBase
{
    private readonly IVectorStore _vectorStore;
    private readonly IAdvancedRetrievalService _advancedRetrievalService;
    private readonly ILogger<RagTestController> _logger;

    public RagTestController(
        IVectorStore vectorStore,
        IAdvancedRetrievalService advancedRetrievalService,
        ILogger<RagTestController> logger)
    {
        _vectorStore = vectorStore;
        _advancedRetrievalService = advancedRetrievalService;
        _logger = logger;
    }

    /// <summary>
    /// Popula a base com dados fictícios para teste de carga.
    /// </summary>
    [HttpPost("seed")]
    public async Task<IActionResult> SeedData([FromBody] SeedRequest request)
    {
        var count = request.Count > 0 ? request.Count : 1000;
        var tenantId = request.TenantId ?? "tenant-stress-01";
        
        _logger.LogInformation("🌱 Iniciando seed de {Count} documentos para o tenant {TenantId}", count, tenantId);
        
        var random = new Random();
        var sw = Stopwatch.StartNew();

        // Gerando em lotes para não estourar a memória
        const int batchSize = 100;
        for (int i = 0; i < count; i += batchSize)
        {
            var currentBatchSize = Math.Min(batchSize, count - i);
            for (int j = 0; j < currentBatchSize; j++)
            {
                var id = Guid.NewGuid().ToString();
                var content = $"Documento de teste número {i + j}. Conteúdo aleatório sobre o sistema agentic e pesquisa híbrida.";
                
                // Gerando embedding sintético (1536 dimensões)
                var embedding = new float[1536];
                for (int k = 0; k < embedding.Length; k++)
                {
                    embedding[k] = (float)random.NextDouble();
                }

                var doc = new EmbeddingDocument
                {
                    Id = id,
                    TenantId = tenantId,
                    Content = content,
                    Type = "test",
                    Collection = "stress-test",
                    Embedding = embedding,
                    IndexedAt = DateTime.UtcNow,
                    Metadata = new Dictionary<string, string>
                    {
                        ["index"] = (i + j).ToString(),
                        ["batch"] = i.ToString()
                    }
                };

                await _vectorStore.UpsertAsync(doc);
            }
            
            _logger.LogInformation("🌱 Seed: {Count} de {Total} processados...", i + currentBatchSize, count);
        }

        sw.Stop();
        _logger.LogInformation("✅ Seed concluído em {Duration}ms", sw.ElapsedMilliseconds);

        return Ok(new { 
            message = $"Seed de {count} documentos concluído com sucesso.",
            durationMs = sw.ElapsedMilliseconds,
            tenantId = tenantId
        });
    }

    /// <summary>
    /// Endpoint de busca híbrida para o k6.
    /// </summary>
    [HttpPost("search")]
    public async Task<IActionResult> Search([FromBody] SearchRequest request)
    {
        if (string.IsNullOrEmpty(request.Query))
            return BadRequest(new { error = "A query não pode ser vazia." });

        var tenantId = request.TenantId ?? "tenant-stress-01";
        
        // Simula o cabeçalho X-Tenant-Id se não vier na request (para manter compatibilidade com o k6)
        if (!Request.Headers.ContainsKey("X-Tenant-Id"))
        {
            Request.Headers["X-Tenant-Id"] = tenantId;
        }

        var ragQuery = new RAGQuery
        {
            Query = request.Query,
            MaxResults = request.TopK > 0 ? request.TopK : 5
        };

        var sw = Stopwatch.StartNew();
        
        // Chama o serviço de busca híbrida (RRF)
        var context = await _advancedRetrievalService.HybridSearchAsync(ragQuery, null, default);
        
        sw.Stop();

        // O k6 espera um array ou um objeto que contenha os resultados.
        // Vamos retornar os chunks encontrados.
        return Ok(context.Chunks.Select(c => new {
            c.Id,
            c.Content,
            Score = c.ReRankedScore,
            c.Metadata
        }));
    }
}

public class SeedRequest
{
    public int Count { get; set; }
    public string? TenantId { get; set; }
}

public class SearchRequest
{
    public string Query { get; set; } = string.Empty;
    public string? TenantId { get; set; }
    public int TopK { get; set; }
    public string Mode { get; set; } = "hybrid";
}
#endif
