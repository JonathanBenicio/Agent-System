using Microsoft.Extensions.Logging;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Services;

/// <summary>
/// Basic implementation of IAgentSandbox for isolated execution testing.
/// </summary>
public class AgentSandbox : IAgentSandbox
{
    private readonly IAgentFactory _agentFactory;
    private readonly ILogger<AgentSandbox> _logger;

    public AgentSandbox(IAgentFactory agentFactory, ILogger<AgentSandbox> logger)
    {
        _agentFactory = agentFactory;
        _logger = logger;
    }

    public Task<SandboxConfig> CreateSandboxAsync(
        string agentName,
        SandboxConfig? config = null,
        CancellationToken ct = default)
    {
        var sandboxId = $"sb-{agentName}-{Guid.NewGuid():N}";
        _logger.LogInformation("🛠️ Created sandbox {SandboxId} for agent {AgentName}", sandboxId, agentName);

        return Task.FromResult(config ?? new SandboxConfig 
        { 
            AgentName = agentName,
            UseMockTools = true
        });
    }

    public async Task<SandboxExecutionResult> ExecuteAsync(
        string sandboxId,
        string userMessage,
        CancellationToken ct = default)
    {
        _logger.LogDebug("Executing in sandbox {SandboxId}: {Message}", sandboxId, userMessage);
        
        // In a full implementation, this would instantiate the agent with mock tools
        // and a custom ChatClient. For this integration, we mock a success response.
        
        return await Task.FromResult(new SandboxExecutionResult
        {
            SandboxId = sandboxId,
            Success = true,
            Response = "Sandbox Execution Result: " + userMessage,
            Duration = TimeSpan.FromMilliseconds(150),
            TotalToolCalls = 1
        });
    }

    public async Task<IReadOnlyList<SandboxExecutionResult>> RunValidationSetAsync(
        string sandboxId,
        IReadOnlyList<string> testPrompts,
        CancellationToken ct = default)
    {
        var results = new List<SandboxExecutionResult>();
        foreach (var prompt in testPrompts)
        {
            results.Add(await ExecuteAsync(sandboxId, prompt, ct));
        }
        return results;
    }

    public Task<SandboxCostEstimate> EstimateCostAsync(
        string agentName,
        string userMessage,
        CancellationToken ct = default)
    {
        return Task.FromResult(new SandboxCostEstimate
        {
            EstimatedInputTokens = userMessage.Length / 4, // Simple heuristic
            EstimatedCostUsd = 0.002
        });
    }

    public Task DestroySandboxAsync(string sandboxId, CancellationToken ct = default)
    {
        _logger.LogInformation("🗑️ Destroyed sandbox {SandboxId}", sandboxId);
        return Task.CompletedTask;
    }
}
