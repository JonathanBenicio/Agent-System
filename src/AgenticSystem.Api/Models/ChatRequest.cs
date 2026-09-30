using System.Text.Json;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Api.Models;

/// <summary>
/// Request payload for chat endpoints (/api/chat and /api/chat/stream).
/// </summary>
public record ChatRequest(
    string Message,
    string? UserId = null,
    string? UserName = null,
    string? TargetAgent = null,
    string? Provider = null,
    string? Model = null,
    string? ApiKey = null,
    Dictionary<string, object>? Context = null,
    string? SessionId = null);

/// <summary>
/// Response payload for the synchronous chat endpoint.
/// </summary>
public record ChatResponse(
    string Response,
    string AgentUsed,
    int AgentTier,
    List<string> ActionsPerformed,
    Dictionary<string, object>? Metadata = null);

/// <summary>
/// Builds LLM preference dictionaries from incoming ChatRequest fields.
/// </summary>
public static class ChatRequestPreferencesBuilder
{
    public static Dictionary<string, object> BuildLlmPreferences(ChatRequest request)
    {
        var preferences = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        if (request.Context is not null)
        {
            foreach (var kv in request.Context)
            {
                preferences[kv.Key] = kv.Value;
            }
        }

        if (!string.IsNullOrWhiteSpace(request.Provider))
        {
            preferences["llm.request.provider"] = request.Provider;
            preferences["llm.session.provider"] = request.Provider;
            preferences["llm.provider"] = request.Provider;
        }

        if (!string.IsNullOrWhiteSpace(request.Model))
        {
            preferences["llm.request.model"] = request.Model;
            preferences["llm.session.model"] = request.Model;
            preferences["llm.model"] = request.Model;
        }

        if (!string.IsNullOrWhiteSpace(request.ApiKey))
        {
            preferences["llm.request.apiKey"] = request.ApiKey;
            preferences["llm.session.apiKey"] = request.ApiKey;
            preferences["llm.apiKey"] = request.ApiKey;
        }

        return preferences;
    }
}
