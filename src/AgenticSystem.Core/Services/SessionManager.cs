using Microsoft.Extensions.Logging;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Services;

public class SessionManager : ISessionManager
{
    private readonly ISessionStore _store;
    private readonly ISessionConsolidator _consolidator;
    private readonly ILogger<SessionManager> _logger;

    public SessionManager(ISessionStore store, ISessionConsolidator consolidator, ILogger<SessionManager> logger)
    {
        _store = store;
        _consolidator = consolidator;
        _logger = logger;
    }

    public async Task<string> StartSessionAsync(UserContext userContext)
    {
        var sessionId = $"session-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid().ToString("N")[..8]}";
        var session = new SessionData
        {
            Id = sessionId,
            UserId = userContext.UserId,
            StartedAt = DateTime.UtcNow,
            Events = new List<AgentEvent>()
        };

        await _store.SaveAsync(session);
        _logger.LogInformation("📂 Session started: {SessionId}", sessionId);
        return sessionId;
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
            var summary = await _consolidator.SummarizeSessionAsync(sessionId, session.Events);
            var insights = await _consolidator.ExtractInsightsAsync(sessionId, session.Events);

            session.IsConsolidated = true;
            session.Summary = summary;
            session.Insights = insights;

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
}
