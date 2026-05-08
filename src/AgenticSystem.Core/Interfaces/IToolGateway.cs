using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Interfaces;

/// <summary>
/// Secure tool execution gateway with validation, policy enforcement,
/// timeout management, and dry-run support.
/// </summary>
public interface IToolGateway
{
    /// <summary>
    /// Executes a tool through the secure pipeline:
    /// 1. Policy check → 2. Schema validation → 3. Input sanitization →
    /// 4. Execution with timeout/retry → 5. Output validation → 6. Audit log
    /// </summary>
    Task<ToolGatewayResult> ExecuteAsync(ITool tool, ToolInput input, ToolGatewayOptions? options = null, CancellationToken ct = default);
}

/// <summary>
/// Options for tool gateway execution.
/// </summary>
public class ToolGatewayOptions
{
    /// <summary>If true, returns the execution plan without actually executing.</summary>
    public bool DryRun { get; set; }

    /// <summary>If true, skips schema validation (use with caution).</summary>
    public bool SkipValidation { get; set; }

    /// <summary>Custom timeout override. If null, uses policy timeout.</summary>
    public TimeSpan? CustomTimeout { get; set; }

    /// <summary>Custom retry count override. If null, uses policy retries.</summary>
    public int? CustomRetries { get; set; }

    /// <summary>Idempotency key for write operations.</summary>
    public string? IdempotencyKey { get; set; }
}

/// <summary>
/// Result of a tool gateway execution.
/// </summary>
public class ToolGatewayResult
{
    public bool Success { get; set; }
    public string? Output { get; set; }
    public string? ErrorMessage { get; set; }
    public bool WasDryRun { get; set; }
    public bool WasCached { get; set; }
    public string? AuditId { get; set; }
    public TimeSpan Latency { get; set; }
    public int AttemptsUsed { get; set; } = 1;
    public ToolExecutionPolicy? AppliedPolicy { get; set; }
    public PolicyEvaluation? PolicyResult { get; set; }
    public List<string> ValidationErrors { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();

    public static ToolGatewayResult Ok(string output, TimeSpan latency, ToolExecutionPolicy? policy = null) => new()
    {
        Success = true,
        Output = output,
        Latency = latency,
        AppliedPolicy = policy
    };

    public static ToolGatewayResult Fail(string error, ToolExecutionPolicy? policy = null) => new()
    {
        Success = false,
        ErrorMessage = error,
        AppliedPolicy = policy
    };

    public static ToolGatewayResult DryRunResult(ToolExecutionPolicy policy, PolicyEvaluation? policyResult = null) => new()
    {
        Success = true,
        WasDryRun = true,
        AppliedPolicy = policy,
        PolicyResult = policyResult,
        Output = $"Dry-run: tool would execute with policy {policy.ToolId}, risk={policy.RiskLevel}, timeout={policy.Timeout.TotalSeconds}s"
    };
}
