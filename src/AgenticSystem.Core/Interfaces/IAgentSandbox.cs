using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Interfaces;

/// <summary>
/// Agent sandbox service for isolated testing and validation.
/// Provides mock tools, simulated RAG, and cost estimation.
/// </summary>
public interface IAgentSandbox
{
    /// <summary>
    /// Creates a sandbox environment for an agent.
    /// </summary>
    Task<SandboxConfig> CreateSandboxAsync(
        string agentName,
        SandboxConfig? config = null,
        CancellationToken ct = default);

    /// <summary>
    /// Executes a prompt in the sandbox environment.
    /// </summary>
    Task<SandboxExecutionResult> ExecuteAsync(
        string sandboxId,
        string userMessage,
        CancellationToken ct = default);

    /// <summary>
    /// Runs a validation dataset against the agent in sandbox mode.
    /// Returns aggregated results.
    /// </summary>
    Task<IReadOnlyList<SandboxExecutionResult>> RunValidationSetAsync(
        string sandboxId,
        IReadOnlyList<string> testPrompts,
        CancellationToken ct = default);

    /// <summary>
    /// Estimates the cost of running a prompt without executing it.
    /// </summary>
    Task<SandboxCostEstimate> EstimateCostAsync(
        string agentName,
        string userMessage,
        CancellationToken ct = default);

    /// <summary>
    /// Destroys a sandbox and its recorded data.
    /// </summary>
    Task DestroySandboxAsync(string sandboxId, CancellationToken ct = default);
}
