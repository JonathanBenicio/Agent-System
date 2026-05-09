namespace AgenticSystem.Core.Models;

// ═══════════════════════════════════════════════════════════
// Agent Sandbox — Isolated Testing Environment
// ═══════════════════════════════════════════════════════════

/// <summary>
/// Configuration for an agent sandbox environment.
/// </summary>
public class SandboxConfig
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string AgentName { get; init; } = string.Empty;
    public bool UseMockTools { get; init; } = true;
    public bool UseMockRAG { get; init; } = true;
    public bool UseMockLLM { get; init; } = false;
    public bool RecordAllInteractions { get; init; } = true;
    public double? MaxBudgetUsd { get; init; }
    public TimeSpan? MaxDuration { get; init; }
    public List<SandboxToolOverride> ToolOverrides { get; init; } = [];
    public Dictionary<string, string> MockData { get; init; } = new();
}

/// <summary>
/// Override for a specific tool in the sandbox.
/// </summary>
public class SandboxToolOverride
{
    public string ToolName { get; init; } = string.Empty;
    public SandboxToolBehavior Behavior { get; init; } = SandboxToolBehavior.Mock;
    public string? MockResponse { get; init; }
    public int? SimulatedLatencyMs { get; init; }
    public bool? SimulateFailure { get; init; }
}

public enum SandboxToolBehavior
{
    Mock,       // Return mock data
    Passthrough,// Use real tool
    Record,     // Use real tool but record input/output
    Replay      // Replay a previously recorded response
}

/// <summary>
/// Result of running an agent in a sandbox.
/// </summary>
public class SandboxExecutionResult
{
    public string SandboxId { get; init; } = string.Empty;
    public string AgentName { get; init; } = string.Empty;
    public bool Success { get; init; }
    public string Response { get; init; } = string.Empty;
    public List<SandboxInteraction> Interactions { get; init; } = [];
    public SandboxCostEstimate CostEstimate { get; init; } = new();
    public TimeSpan Duration { get; init; }
    public int TotalToolCalls { get; init; }
    public int TotalLLMCalls { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// A recorded interaction during sandbox execution.
/// </summary>
public class SandboxInteraction
{
    public int Sequence { get; init; }
    public SandboxInteractionType Type { get; init; }
    public string Description { get; init; } = string.Empty;
    public string? Input { get; init; }
    public string? Output { get; init; }
    public bool WasMocked { get; init; }
    public TimeSpan Duration { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

public enum SandboxInteractionType
{
    LLMCall,
    ToolCall,
    RAGRetrieval,
    MemoryAccess,
    ApprovalRequest,
    ExternalApi
}

/// <summary>
/// Cost estimate for sandbox execution.
/// </summary>
public class SandboxCostEstimate
{
    public int EstimatedInputTokens { get; init; }
    public int EstimatedOutputTokens { get; init; }
    public double EstimatedCostUsd { get; init; }
    public string PricingModel { get; init; } = string.Empty;
}
