using System.Text.Json;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.LLM.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.Persistence;
using AgenticSystem.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace AgenticSystem.Infrastructure.RAG;

public class PostgresSemanticCacheService : ISemanticCacheService
{
    private readonly IDbContextFactory<AgenticDbContext> _dbContextFactory;
    private readonly IEmbeddingProvider _embeddingProvider;
    private readonly ILogger<PostgresSemanticCacheService> _logger;

    private const string CacheCollection = "semantic_cache";

    public PostgresSemanticCacheService(
        IDbContextFactory<AgenticDbContext> dbContextFactory,
        IEmbeddingProvider embeddingProvider,
        ILogger<PostgresSemanticCacheService> logger)
    {
        _dbContextFactory = dbContextFactory;
        _embeddingProvider = embeddingProvider;
        _logger = logger;
    }

    public async Task<SemanticCacheResult> GetCachedResponseAsync(string prompt, string agentName, double similarityThreshold = 0.95, CancellationToken ct = default)
    {
        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            
            // 1. Generate embedding for the prompt
            var promptEmbedding = await _embeddingProvider.GenerateEmbeddingAsync(prompt, ct);
            var queryVector = new Vector(promptEmbedding);

            await using var db = await _dbContextFactory.CreateDbContextAsync(ct);

            // 2. Perform cosine similarity search specifically on the cache collection
            var candidates = await db.VectorDocuments
                .AsNoTracking()
                .Where(x => x.Collection == CacheCollection && x.Type == agentName)
                .OrderBy(x => x.Embedding!.CosineDistance(queryVector))
                .Take(5)
                .Select(x => new 
                { 
                    x.Id, 
                    x.Content, // Prompt
                    x.MetadataJson, 
                    Distance = x.Embedding!.CosineDistance(queryVector) 
                })
                .ToListAsync(ct);

            // Distance is 0 for identical, higher for different. 
            // Similarity = 1 - Distance
            foreach (var candidate in candidates)
            {
                var similarity = 1.0 - candidate.Distance;
                if (similarity >= similarityThreshold)
                {
                    // Hit!
                    var metadata = JsonSerializer.Deserialize<Dictionary<string, string>>(candidate.MetadataJson) ?? new();
                    
                    if (metadata.TryGetValue("ExpiresAt", out var expiresAtStr) && DateTime.TryParse(expiresAtStr, out var expiresAt))
                    {
                        if (DateTime.UtcNow > expiresAt)
                        {
                            // Expired. We should clean it up asynchronously but for now just skip.
                            continue;
                        }
                    }

                    _logger.LogInformation("🎯 Semantic Cache HIT for agent {Agent}. Similarity: {Sim:P2}. Latency: {Ms}ms", agentName, similarity, sw.ElapsedMilliseconds);
                    
                    metadata.TryGetValue("CachedResponse", out var response);
                    
                    return new SemanticCacheResult
                    {
                        IsHit = true,
                        CachedResponse = response,
                        SimilarityScore = similarity,
                        Metadata = metadata
                    };
                }
            }

            _logger.LogDebug("Miss no Semantic Cache para {Agent}. Maior similaridade: {MaxSim:P2}", 
                agentName, candidates.Count > 0 ? 1.0 - candidates.First().Distance : 0);

            return new SemanticCacheResult { IsHit = false };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao consultar Semantic Cache.");
            return new SemanticCacheResult { IsHit = false };
        }
    }

    public async Task SetCachedResponseAsync(string prompt, string response, string agentName, TimeSpan? ttl = null, CancellationToken ct = default)
    {
        try
        {
            var promptEmbedding = await _embeddingProvider.GenerateEmbeddingAsync(prompt, ct);
            
            var expiresAt = DateTime.UtcNow.Add(ttl ?? TimeSpan.FromDays(1));
            var metadata = new Dictionary<string, string>
            {
                ["AgentName"] = agentName,
                ["CachedResponse"] = response,
                ["ExpiresAt"] = expiresAt.ToString("O")
            };

            var entity = new VectorDocumentEntity
            {
                Id = Guid.NewGuid().ToString(),
                Content = prompt, // We store the prompt in Content to display it if needed
                Type = agentName, // We store agentName in Type for fast filtering
                Collection = CacheCollection,
                Embedding = new Vector(promptEmbedding),
                MetadataJson = JsonSerializer.Serialize(metadata),
                IndexedAt = DateTime.UtcNow
            };

            await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
            db.VectorDocuments.Add(entity);
            await db.SaveChangesAsync(ct);
            
            _logger.LogDebug("Cache Semântico salvo para {Agent}. TTL: {TTL}", agentName, ttl ?? TimeSpan.FromDays(1));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao gravar no Semantic Cache.");
        }
    }

    public async Task InvalidateAgentCacheAsync(string agentName, CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        
        var expiredDocs = await db.VectorDocuments
            .Where(x => x.Collection == CacheCollection && x.Type == agentName)
            .ToListAsync(ct);

        if (expiredDocs.Count > 0)
        {
            db.VectorDocuments.RemoveRange(expiredDocs);
            await db.SaveChangesAsync(ct);
            _logger.LogInformation("Cache Semântico do agente {Agent} invalidado. {Count} itens removidos.", agentName, expiredDocs.Count);
        }
    }
}
