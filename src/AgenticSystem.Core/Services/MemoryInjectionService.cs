using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Services;

/// <summary>
/// ML14 — Memory Injection: vetoriza insights de sessões e injeta contexto relevante no system prompt.
/// </summary>
public interface IMemoryInjectionService
{
    Task<VectorizeInsightsResult> VectorizeInsightsAsync(SessionInsights insights, string userId, string tenantId, string sessionId, CancellationToken ct = default);
    Task<string> BuildMemoryContextAsync(string userQuery, string userId, string tenantId, int maxMemories = 10, CancellationToken ct = default);
}

public record VectorizeInsightsResult(int DocumentsCreated, string[] Types);

public class MemoryInjectionService : IMemoryInjectionService
{
    private readonly IVectorStore _vectorStore;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<MemoryInjectionService> _logger;
    private readonly MemoryCacheEntryOptions _cacheOptions;

    public MemoryInjectionService(IVectorStore vectorStore, IMemoryCache memoryCache, ILogger<MemoryInjectionService> logger)
    {
        _vectorStore = vectorStore;
        _memoryCache = memoryCache;
        _logger = logger;
        _cacheOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
            Size = 1,
        };
    }

    public async Task<VectorizeInsightsResult> VectorizeInsightsAsync(SessionInsights insights, string userId, string tenantId, string sessionId, CancellationToken ct = default)
    {
        var documents = new List<EmbeddingDocument>();
        var types = new List<string>();

        foreach (var fact in insights.Facts)
        {
            documents.Add(new EmbeddingDocument
            {
                Id = $"memory_{sessionId}_fact_{Guid.NewGuid():N}",
                TenantId = tenantId,
                Content = fact,
                Type = "memory",
                Collection = "domain",
                Metadata = new Dictionary<string, string>
                {
                    ["memoryType"] = "fact",
                    ["userId"] = userId,
                    ["tenantId"] = tenantId,
                    ["sessionId"] = sessionId,
                    ["createdAt"] = DateTime.UtcNow.ToString("O"),
                },
            });
            types.Add("fact");
        }

        foreach (var decision in insights.Decisions)
        {
            documents.Add(new EmbeddingDocument
            {
                Id = $"memory_{sessionId}_decision_{Guid.NewGuid():N}",
                TenantId = tenantId,
                Content = decision,
                Type = "memory",
                Collection = "decisions",
                Metadata = new Dictionary<string, string>
                {
                    ["memoryType"] = "decision",
                    ["userId"] = userId,
                    ["tenantId"] = tenantId,
                    ["sessionId"] = sessionId,
                    ["createdAt"] = DateTime.UtcNow.ToString("O"),
                },
            });
            types.Add("decision");
        }

        foreach (var preference in insights.Preferences)
        {
            documents.Add(new EmbeddingDocument
            {
                Id = $"memory_{sessionId}_preference_{Guid.NewGuid():N}",
                TenantId = tenantId,
                Content = preference,
                Type = "memory",
                Collection = "domain",
                Metadata = new Dictionary<string, string>
                {
                    ["memoryType"] = "preference",
                    ["userId"] = userId,
                    ["tenantId"] = tenantId,
                    ["sessionId"] = sessionId,
                    ["createdAt"] = DateTime.UtcNow.ToString("O"),
                },
            });
            types.Add("preference");
        }

        foreach (var actionItem in insights.ActionItems)
        {
            documents.Add(new EmbeddingDocument
            {
                Id = $"memory_{sessionId}_action_{Guid.NewGuid():N}",
                TenantId = tenantId,
                Content = actionItem,
                Type = "memory",
                Collection = "domain",
                Metadata = new Dictionary<string, string>
                {
                    ["memoryType"] = "actionItem",
                    ["userId"] = userId,
                    ["tenantId"] = tenantId,
                    ["sessionId"] = sessionId,
                    ["createdAt"] = DateTime.UtcNow.ToString("O"),
                },
            });
            types.Add("actionItem");
        }

        if (documents.Count == 0)
        {
            return new VectorizeInsightsResult(0, Array.Empty<string>());
        }

        var upserted = 0;
        foreach (var doc in documents)
        {
            await _vectorStore.UpsertAsync(doc);
            upserted++;
        }

        _memoryCache.Set(("memory-generation", userId, tenantId), Guid.NewGuid(), _cacheOptions);

        _logger.LogInformation("🧠 Vectorized {Count} insights for session {SessionId}", upserted, sessionId);

        return new VectorizeInsightsResult(upserted, types.Distinct().ToArray());
    }

    public async Task<string> BuildMemoryContextAsync(string userQuery, string userId, string tenantId, int maxMemories = 10, CancellationToken ct = default)
    {
        var generation = _memoryCache.GetOrCreate(("memory-generation", userId, tenantId), entry =>
        {
            entry.SetOptions(_cacheOptions);
            return Guid.NewGuid();
        });
        var cacheKey = ("memory-context", userId, tenantId, userQuery, maxMemories, generation);

        if (_memoryCache.TryGetValue(cacheKey, out var cached) && cached is CachedMemoryContext cachedResult)
        {
            _logger.LogDebug("🧠 Memory context cache hit for user {UserId}", userId);
            return cachedResult.Context;
        }

        try
        {
            var filters = new Dictionary<string, string>
            {
                ["type"] = "memory",
                ["userId"] = userId,
                ["tenantId"] = tenantId,
            };

            var result = await _vectorStore.SearchWithFiltersAsync(userQuery, filters);

            if (result.Matches.Count == 0)
            {
                _memoryCache.Set(cacheKey, new CachedMemoryContext(string.Empty), _cacheOptions);
                return string.Empty;
            }

            var facts = new List<string>();
            var decisions = new List<string>();
            var preferences = new List<string>();
            var actionItems = new List<string>();

            foreach (var match in result.Matches.Take(maxMemories))
            {
                var memoryType = match.Metadata.TryGetValue("memoryType", out var mt) ? mt : "unknown";

                switch (memoryType)
                {
                    case "fact": facts.Add(match.Content); break;
                    case "decision": decisions.Add(match.Content); break;
                    case "preference": preferences.Add(match.Content); break;
                    case "actionItem": actionItems.Add(match.Content); break;
                }
            }

            if (facts.Count == 0 && decisions.Count == 0 && preferences.Count == 0 && actionItems.Count == 0)
            {
                _memoryCache.Set(cacheKey, new CachedMemoryContext(string.Empty), _cacheOptions);
                return string.Empty;
            }

            var sb = new StringBuilder();
            sb.AppendLine("\nRELEVANT CONTEXT FROM PREVIOUS INTERACTIONS:");

            if (facts.Count > 0)
            {
                sb.AppendLine("- Facts:");
                foreach (var f in facts) sb.AppendLine($"  • {f}");
            }

            if (decisions.Count > 0)
            {
                sb.AppendLine("- Decisions:");
                foreach (var d in decisions) sb.AppendLine($"  • {d}");
            }

            if (preferences.Count > 0)
            {
                sb.AppendLine("- Preferences:");
                foreach (var p in preferences) sb.AppendLine($"  • {p}");
            }

            if (actionItems.Count > 0)
            {
                sb.AppendLine("- Action Items:");
                foreach (var a in actionItems) sb.AppendLine($"  • {a}");
            }

            var context = sb.ToString();
            _memoryCache.Set(cacheKey, new CachedMemoryContext(context), _cacheOptions);

            return context;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to build memory context for user {UserId}", userId);
            return string.Empty;
        }
    }
}

internal record CachedMemoryContext(string Context);
