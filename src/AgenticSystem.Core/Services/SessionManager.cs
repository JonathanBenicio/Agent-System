using Microsoft.Extensions.Logging;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Services;

public class SessionManager : ISessionManager
{
    private readonly ISessionStore _store;
    private readonly ISessionConsolidator _consolidator;
    private readonly IMemoryInjectionService? _memoryInjection;
    private readonly ILogger<SessionManager> _logger;
    private readonly ISemanticCompressor? _semanticCompressor;
    private readonly ITenantStore? _tenantStore;

    public SessionManager(
        ISessionStore store,
        ISessionConsolidator consolidator,
        ILogger<SessionManager> logger,
        ISemanticCompressor? semanticCompressor = null,
        IMemoryInjectionService? memoryInjection = null,
        ITenantStore? tenantStore = null)
    {
        _store = store;
        _consolidator = consolidator;
        _memoryInjection = memoryInjection;
        _logger = logger;
        _semanticCompressor = semanticCompressor;
        _tenantStore = tenantStore;
    }

    public async Task<string> StartSessionAsync(UserContext userContext, string? sessionId = null)
    {
        if (!string.IsNullOrWhiteSpace(sessionId))
        {
            var existingSession = await _store.GetAsync(sessionId);
            if (existingSession is not null)
            {
                var userTenantId = userContext.TenantId;
                if (existingSession.UserId != userContext.UserId || existingSession.TenantId != userTenantId)
                    throw new UnauthorizedAccessException("Session belongs to another user or tenant.");
                if (existingSession.EndedAt is not null)
                    throw new InvalidOperationException("Ended sessions cannot be resumed.");
                _logger.LogInformation("📂 Reusing existing session: {SessionId}", sessionId);
                return sessionId;
            }
        }

        var newSessionId = $"session-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}";
        var session = new SessionData
        {
            Id = newSessionId,
            UserId = userContext.UserId,
            TenantId = userContext.TenantId,
            StartedAt = DateTime.UtcNow,
            RuntimeSettings = BuildRuntimeSettings(userContext),
            Events = new List<AgentEvent>()
        };

        if (_tenantStore is not null)
        {
            var tenant = await _tenantStore.GetByIdAsync(userContext.TenantId)
                ?? throw new UnauthorizedAccessException("Tenant not found.");
            if (!tenant.IsActive) throw new UnauthorizedAccessException("Tenant inactive.");
            var planCeiling = tenant.Plan switch
            {
                TenantPlan.Pro => TenantLimits.ProTier().MaxConcurrentSessions,
                TenantPlan.Enterprise => TenantLimits.EnterpriseTier().MaxConcurrentSessions,
                _ => TenantLimits.FreeTier().MaxConcurrentSessions
            };
            var configured = tenant.Limits.MaxConcurrentSessions;
            var limit = configured > 0 && planCeiling > 0 ? Math.Min(configured, planCeiling)
                : configured > 0 ? configured : planCeiling;
            if (!await _store.TryCreateAsync(session, limit))
                throw new AgenticSystem.Core.Exceptions.QuotaExceededException("Concurrent session limit exceeded.");
        }
        else
        {
            // Legacy test fixtures without a tenant registry do not represent runtime enforcement.
            await _store.SaveAsync(session);
        }
        _logger.LogInformation("📂 Session started: {SessionId}", newSessionId);
        return newSessionId;
    }

    public async Task AddEventAsync(string sessionId, AgentEvent agentEvent)
    {
        var session = await _store.GetAsync(sessionId);
        if (session is not null)
        {
            agentEvent.SessionId = sessionId;
            session.Events.Add(agentEvent);
            await _store.SaveAsync(session);
            _logger.LogDebug("📝 Event added to session {SessionId}: {Agent}", sessionId, agentEvent.AgentName);
        }
    }

    public async Task ConsolidateSessionAsync(string sessionId)
    {
        var session = await _store.GetAsync(sessionId);
        if (session is not null)
        {
            var summary = await _consolidator.SummarizeSessionAsync(sessionId, session.Events, session.UserId, session.TenantId);
            var insights = await _consolidator.ExtractInsightsAsync(sessionId, session.Events, session.UserId, session.TenantId);

            if (_memoryInjection != null)
            {
                try
                {
                    var result = await _memoryInjection.VectorizeInsightsAsync(insights, session.UserId, session.TenantId, sessionId);
                    _logger.LogDebug("🧠 Vectorized {Count} insights for session {SessionId}", result.DocumentsCreated, sessionId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to vectorize insights for session {SessionId}", sessionId);
                }
            }

            session.IsConsolidated = true;
            session.Summary = summary;
            session.Insights = insights;

            if (_semanticCompressor != null)
            {
                try
                {
                    await _semanticCompressor.CompressSessionAsync(sessionId);
                    _logger.LogDebug("🗜️ Session {SessionId} semantically compressed", sessionId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Semantic compression failed for session {SessionId}", sessionId);
                }
            }

            await _store.SaveAsync(session);

            _logger.LogInformation("🔒 Session consolidated: {SessionId} ({EventCount} events, {Topics} topics)",
                sessionId, session.Events.Count, summary?.TopicsDiscussed?.Count ?? 0);
        }
    }

    public async Task<List<AgentEvent>> GetRecentEventsAsync(string sessionId, int count = 10)
    {
        var session = await _store.GetAsync(sessionId);
        if (session is not null)
        {
            return session.Events
                .OrderByDescending(e => e.Timestamp)
                .Take(count)
                .ToList();
        }
        return new List<AgentEvent>();
    }

    public async Task EndSessionAsync(string sessionId)
    {
        var session = await _store.GetAsync(sessionId);
        if (session is not null)
        {
            session.EndedAt = DateTime.UtcNow;
            await _store.SaveAsync(session);
            _logger.LogInformation("🏁 Session ended: {SessionId} (Duration: {Duration})",
                sessionId, session.EndedAt - session.StartedAt);
        }
    }

    public async Task<string> GetMemoryContextAsync(string userQuery, string userId, string tenantId, CancellationToken ct = default)
    {
        if (_memoryInjection == null)
        {
            return string.Empty;
        }

        try
        {
            return await _memoryInjection.BuildMemoryContextAsync(userQuery, userId, tenantId, ct: ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to build memory context for user {UserId}", userId);
            return string.Empty;
        }
    }

    private static Dictionary<string, string> BuildRuntimeSettings(UserContext userContext)
    {
        var settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        CopyPreference(userContext.Preferences, settings, "llm.session.provider");
        CopyPreference(userContext.Preferences, settings, "llm.session.model");

        if (!settings.ContainsKey("llm.session.provider"))
            CopyPreference(userContext.Preferences, settings, "llm.provider", "llm.session.provider");

        if (!settings.ContainsKey("llm.session.model"))
            CopyPreference(userContext.Preferences, settings, "llm.model", "llm.session.model");


        return settings;
    }

    private static void CopyPreference(
        IReadOnlyDictionary<string, object> source,
        IDictionary<string, string> destination,
        string sourceKey,
        string? targetKey = null)
    {
        if (!source.TryGetValue(sourceKey, out var raw) || raw is null)
            return;

        var value = raw.ToString();
        if (string.IsNullOrWhiteSpace(value))
            return;

        destination[targetKey ?? sourceKey] = value;
    }
}
