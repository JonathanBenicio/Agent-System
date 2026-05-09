using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Core.Services;

/// <summary>
/// Executes fallback strategy when the primary agent or provider fails.
/// Integrates with SmartRouter's FallbackChain and ServiceGateway's circuit breaker.
/// Supports automatic retry with alternate models and graceful degradation.
/// </summary>
public class FallbackExecutor
{
    private readonly ISmartRouter _router;
    private readonly IAgentFactory _agentFactory;
    private readonly IAuditLog _auditLog;
    private readonly ILogger<FallbackExecutor> _logger;

    public FallbackExecutor(
        ISmartRouter router,
        IAgentFactory agentFactory,
        IAuditLog auditLog,
        ILogger<FallbackExecutor> logger)
    {
        _router = router;
        _agentFactory = agentFactory;
        _auditLog = auditLog;
        _logger = logger;
    }

    /// <summary>
    /// Attempts to execute the primary action. On failure, iterates through the fallback chain.
    /// Returns the result from the first successful execution.
    /// </summary>
    public async Task<FallbackResult<T>> ExecuteWithFallbackAsync<T>(
        RoutingDecision routing,
        Func<IAgent, Task<T>> executeAction,
        CancellationToken ct = default)
    {
        var attempts = new List<FallbackAttempt>();
        var allCandidates = new List<string> { routing.PrimaryAgent };
        allCandidates.AddRange(routing.FallbackChain);

        foreach (var candidateName in allCandidates)
        {
            try
            {
                var agent = await _agentFactory.ResolveAgentAsync(new AgentInfo
                {
                    Name = candidateName,
                    Domain = "general"
                });

                if (!agent.IsActive)
                {
                    _logger.LogDebug("Skipping inactive agent {Agent}", candidateName);
                    attempts.Add(new FallbackAttempt
                    {
                        AgentName = candidateName,
                        Success = false,
                        ErrorMessage = "Agent is inactive"
                    });
                    continue;
                }

                var result = await executeAction(agent);

                attempts.Add(new FallbackAttempt
                {
                    AgentName = candidateName,
                    Success = true
                });

                var usedFallback = candidateName != routing.PrimaryAgent;
                if (usedFallback)
                {
                    _logger.LogWarning(
                        "Fallback to {Agent} succeeded (primary: {Primary})",
                        candidateName, routing.PrimaryAgent);

                    await _auditLog.RecordAsync(new AuditEntry
                    {
                        Category = AuditCategory.SystemEvent,
                        Action = "Fallback.Activated",
                        AgentName = candidateName,
                        Description = $"Fallback to '{candidateName}' succeeded after primary '{routing.PrimaryAgent}' failed.",
                        Metadata = new Dictionary<string, object>
                        {
                            ["primaryAgent"] = routing.PrimaryAgent,
                            ["fallbackAgent"] = candidateName,
                            ["attemptNumber"] = attempts.Count,
                            ["totalCandidates"] = allCandidates.Count
                        }
                    }, ct);
                }

                return new FallbackResult<T>
                {
                    Success = true,
                    Result = result,
                    UsedFallback = usedFallback,
                    SelectedAgent = candidateName,
                    Attempts = attempts
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Agent {Agent} failed during fallback execution", candidateName);
                attempts.Add(new FallbackAttempt
                {
                    AgentName = candidateName,
                    Success = false,
                    ErrorMessage = ex.Message
                });
            }
        }

        // All candidates exhausted
        _logger.LogError("All fallback candidates exhausted. Primary: {Primary}", routing.PrimaryAgent);

        await _auditLog.RecordAsync(new AuditEntry
        {
            Category = AuditCategory.SystemEvent,
            Action = "Fallback.Exhausted",
            AgentName = routing.PrimaryAgent,
            Description = $"All {allCandidates.Count} fallback candidates exhausted for primary '{routing.PrimaryAgent}'.",
            Success = false,
            Metadata = new Dictionary<string, object>
            {
                ["candidates"] = string.Join(", ", allCandidates),
                ["totalAttempts"] = attempts.Count
            }
        }, ct);

        return new FallbackResult<T>
        {
            Success = false,
            UsedFallback = true,
            SelectedAgent = routing.PrimaryAgent,
            Attempts = attempts,
            ErrorMessage = $"All {allCandidates.Count} fallback candidates failed."
        };
    }
}

/// <summary>
/// Result of a fallback execution chain.
/// </summary>
public class FallbackResult<T>
{
    public bool Success { get; init; }
    public T? Result { get; init; }
    public bool UsedFallback { get; init; }
    public string SelectedAgent { get; init; } = string.Empty;
    public string? ErrorMessage { get; init; }
    public List<FallbackAttempt> Attempts { get; init; } = [];
}

public class FallbackAttempt
{
    public string AgentName { get; init; } = string.Empty;
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
}
