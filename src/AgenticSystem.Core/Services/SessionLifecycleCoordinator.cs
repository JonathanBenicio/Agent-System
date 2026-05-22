using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Services;

/// <summary>
/// Coordinates the full lifecycle of a session by unifying ISessionManager,
/// IAgentRuntimeCoordinator, ITenantIsolationEnforcer, and IEventPublisher.
/// </summary>
public class SessionLifecycleCoordinator : ISessionLifecycleCoordinator
{
    private readonly ISessionManager _sessionManager;
    private readonly IAgentRuntimeCoordinator _runtimeCoordinator;
    private readonly ITenantIsolationEnforcer? _isolationEnforcer;
    private readonly IEventPublisher? _eventPublisher;
    private readonly ILogger<SessionLifecycleCoordinator> _logger;

    public SessionLifecycleCoordinator(
        ISessionManager sessionManager,
        IAgentRuntimeCoordinator runtimeCoordinator,
        ILogger<SessionLifecycleCoordinator> logger,
        ITenantIsolationEnforcer? isolationEnforcer = null,
        IEventPublisher? eventPublisher = null)
    {
        _sessionManager = sessionManager;
        _runtimeCoordinator = runtimeCoordinator;
        _logger = logger;
        _isolationEnforcer = isolationEnforcer;
        _eventPublisher = eventPublisher;
    }

    public async Task<bool> CanStartSessionAsync(string tenantId)
    {
        if (_isolationEnforcer == null || string.IsNullOrEmpty(tenantId))
            return true;

        return await _isolationEnforcer.CanStartSessionAsync(tenantId);
    }

    public async Task<string> StartSessionAsync(UserContext context, string? sessionId = null, CancellationToken ct = default)
    {
        var resolvedSessionId = await _sessionManager.StartSessionAsync(context, sessionId);
        context.Preferences["sessionId"] = resolvedSessionId;

        if (_eventPublisher != null)
        {
            await _eventPublisher.PublishAsync(new SessionCreatedEvent(resolvedSessionId, context.UserId, context.TenantId), ct);
        }

        return resolvedSessionId;
    }

    public IDisposable BeginExecutionScope(string sessionId, UserContext context)
    {
        return _runtimeCoordinator.BeginExecutionScope(sessionId, context);
    }

    public async Task EndSessionAsync(string sessionId, UserContext context, CancellationToken ct = default)
    {
        await _sessionManager.EndSessionAsync(sessionId);

        if (_eventPublisher != null)
        {
            await _eventPublisher.PublishAsync(new SessionEndedEvent(sessionId, context.UserId, context.TenantId), ct);
        }
    }

    public async Task PublishEventAsync(AgentStreamEvent streamEvent, CancellationToken ct = default)
    {
        await _runtimeCoordinator.PublishEventAsync(streamEvent, ct);
    }

    public IAsyncEnumerable<AgentStreamEvent> StreamAsync(
        string sessionId,
        UserContext context,
        Func<CancellationToken, Task<AgentResponse>> executeAsync,
        CancellationToken ct = default)
    {
        return _runtimeCoordinator.StreamAsync(sessionId, context, executeAsync, ct);
    }
}
