using System.Text;
using System.Text.Json;
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
    private readonly ILogger<MemoryInjectionService> _logger;

    public MemoryInjectionService(IVectorStore vectorStore, ILogger<MemoryInjectionService> logger)
    {
        _vectorStore = vectorStore;
        _logger = logger;
    }

    public async Task<VectorizeInsightsResult> VectorizeInsightsAsync(SessionInsights insights, string userId, string tenantId, string sessionId, CancellationToken ct = default)
    {
        var documents = new List<VectorDocument>();
        var types = new List<string>();

        foreach (var fact in insights.Facts)
        {
            documents.Add(new VectorDocument
            {
                Id = $"memory_{sessionId}_fact_{Guid.NewGuid():N}",
                Content = fact,
                Type = "memory",
                Collection = "domain",
                MetadataJson = JsonSerializer.Serialize(new Dictionary<string, object>
                {
                    ["memoryType"] = "fact",
                    ["userId"] = userId,
                    ["tenantId"] = tenantId,
                    ["sessionId"] = sessionId,
                    ["createdAt"] = DateTime.UtcNow,
                }),
            });
            types.Add("fact");
        }

        foreach (var decision in insights.Decisions)
        {
            documents.Add(new VectorDocument
            {
                Id = $"memory_{sessionId}_decision_{Guid.NewGuid():N}",
                Content = decision,
                Type = "memory",
                Collection = "decisions",
                MetadataJson = JsonSerializer.Serialize(new Dictionary<string, object>
                {
                    ["memoryType"] = "decision",
                    ["userId"] = userId,
                    ["tenantId"] = tenantId,
                    ["sessionId"] = sessionId,
                    ["createdAt"] = DateTime.UtcNow,
                }),
            });
            types.Add("decision");
        }

        foreach (var preference in insights.Preferences)
        {
            documents.Add(new VectorDocument
            {
                Id = $"memory_{sessionId}_preference_{Guid.NewGuid():N}",
                Content = preference,
                Type = "memory",
                Collection = "domain",
                MetadataJson = JsonSerializer.Serialize(new Dictionary<string, object>
                {
                    ["memoryType"] = "preference",
                    ["userId"] = userId,
                    ["tenantId"] = tenantId,
                    ["sessionId"] = sessionId,
                    ["createdAt"] = DateTime.UtcNow,
                }),
            });
            types.Add("preference");
        }

        foreach (var actionItem in insights.ActionItems)
        {
            documents.Add(new VectorDocument
            {
                Id = $"memory_{sessionId}_action_{Guid.NewGuid():N}",
                Content = actionItem,
                Type = "memory",
                Collection = "domain",
                MetadataJson = JsonSerializer.Serialize(new Dictionary<string, object>
                {
                    ["memoryType"] = "actionItem",
                    ["userId"] = userId,
                    ["tenantId"] = tenantId,
                    ["sessionId"] = sessionId,
                    ["createdAt"] = DateTime.UtcNow,
                }),
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
            await _vectorStore.UpsertAsync(doc, ct);
            upserted++;
        }

        _logger.LogInformation("🧠 Vectorized {Count} insights for session {SessionId}", upserted, sessionId);

        return new VectorizeInsightsResult(upserted, types.Distinct().ToArray());
    }

    public async Task<string> BuildMemoryContextAsync(string userQuery, string userId, string tenantId, int maxMemories = 10, CancellationToken ct = default)
    {
        try
        {
            var results = await _vectorStore.SearchWithFiltersAsync(
                query: userQuery,
                topK: maxMemories,
                collection: null,
                filters: new Dictionary<string, object>
                {
                    ["type"] = "memory",
                    ["userId"] = userId,
                    ["tenantId"] = tenantId,
                },
                ct);

            if (results.Count == 0)
            {
                return string.Empty;
            }

            var facts = new List<string>();
            var decisions = new List<string>();
            var preferences = new List<string>();
            var actionItems = new List<string>();

            foreach (var result in results)
            {
                using var doc = JsonDocument.Parse(result.MetadataJson);
                var root = doc.RootElement;
                var memoryType = root.TryGetProperty("memoryType", out var mt) ? mt.GetString() : "unknown";

                switch (memoryType)
                {
                    case "fact": facts.Add(result.Content); break;
                    case "decision": decisions.Add(result.Content); break;
                    case "preference": preferences.Add(result.Content); break;
                    case "actionItem": actionItems.Add(result.Content); break;
                }
            }

            if (facts.Count == 0 && decisions.Count == 0 && preferences.Count == 0 && actionItems.Count == 0)
            {
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

            return sb.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to build memory context for user {UserId}", userId);
            return string.Empty;
        }
    }
}
