using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Interfaces;

/// <summary>
/// Coordinates the full lifecycle of a session: tenant quota checks, session start/end,
/// execution scope creation, and event publishing (e.g., SignalR notifications).
/// Replaces the direct injection of ISessionManager, IAgentRuntimeCoordinator,
/// ITenantIsolationEnforcer, and IEventPublisher in the MetaAgentOrchestrator.
/// </summary>
public interface ISessionLifecycleCoordinator
{
    /// <summary>
    /// Starts a new session, checking tenant quotas and publishing creation events.
    /// </summary>
    /// <returns>The session ID for the newly created session.</returns>
    Task<string> StartSessionAsync(UserContext context, CancellationToken ct = default);

    /// <summary>
    /// Creates a runtime execution scope for the given session.
    /// </summary>
    IDisposable BeginExecutionScope(string sessionId, UserContext context);

    /// <summary>
    /// Ends the session and publishes completion events.
    /// </summary>
    Task EndSessionAsync(string sessionId, UserContext context, CancellationToken ct = default);

    /// <summary>
    /// Publishes a stream event through the runtime coordinator.
    /// </summary>
    Task PublishEventAsync(AgentStreamEvent streamEvent, CancellationToken ct = default);

    /// <summary>
    /// Streams the execution of a delegate within a coordinated session scope.
    /// </summary>
    IAsyncEnumerable<AgentStreamEvent> StreamAsync(
        string sessionId,
        UserContext context,
        Func<CancellationToken, Task<AgentResponse>> executeAsync,
        CancellationToken ct = default);

    /// <summary>
    /// Checks if the tenant can start a new session (quota enforcement).
    /// Returns false if the concurrent session limit has been reached.
    /// </summary>
    Task<bool> CanStartSessionAsync(string tenantId);
}
