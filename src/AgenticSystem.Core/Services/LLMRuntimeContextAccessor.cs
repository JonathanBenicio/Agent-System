using System.Threading;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Services;

public sealed class LLMRuntimeContextAccessor : ILLMRuntimeContextAccessor
{
    private static readonly AsyncLocal<LLMRuntimeContext?> CurrentContext = new();

    public LLMRuntimeContext? Current => CurrentContext.Value;

    public IDisposable BeginScope(UserContext userContext, string? sessionId = null)
    {
        var previous = CurrentContext.Value;
        CurrentContext.Value = BuildContext(userContext, sessionId);
        return new Scope(() => CurrentContext.Value = previous);
    }

    private static LLMRuntimeContext BuildContext(UserContext userContext, string? sessionId)
    {
        return new LLMRuntimeContext
        {
            UserId = userContext.UserId,
            TenantId = string.IsNullOrWhiteSpace(userContext.TenantId) ? Tenant.DefaultTenantId : userContext.TenantId,
            SessionId = sessionId,
            RequestProvider = ReadPreference(userContext.Preferences, "llm.request.provider") ?? ReadPreference(userContext.Preferences, "llm.provider"),
            RequestModel = ReadPreference(userContext.Preferences, "llm.request.model") ?? ReadPreference(userContext.Preferences, "llm.model"),
            RequestApiKey = ReadPreference(userContext.Preferences, "llm.request.apiKey") ?? ReadPreference(userContext.Preferences, "llm.apiKey"),
            RequestApiKeyId = ReadPreference(userContext.Preferences, "llm.request.apiKeyId"),
            SessionProvider = ReadPreference(userContext.Preferences, "llm.session.provider") ?? ReadPreference(userContext.Preferences, "llm.provider"),
            SessionModel = ReadPreference(userContext.Preferences, "llm.session.model") ?? ReadPreference(userContext.Preferences, "llm.model"),
            SessionApiKey = ReadPreference(userContext.Preferences, "llm.session.apiKey") ?? ReadPreference(userContext.Preferences, "llm.apiKey")
        };
    }

    private static string? ReadPreference(IReadOnlyDictionary<string, object> preferences, string key)
    {
        if (!preferences.TryGetValue(key, out var raw) || raw is null)
            return null;

        var value = raw.ToString();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private sealed class Scope : IDisposable
    {
        private readonly Action _restore;
        private int _disposed;

        public Scope(Action restore)
        {
            _restore = restore;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                _restore();
            }
        }
    }
}
