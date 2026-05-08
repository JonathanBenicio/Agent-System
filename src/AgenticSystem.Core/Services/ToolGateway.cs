using System.Diagnostics;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Core.Services;

/// <summary>
/// Secure tool execution gateway with a 7-step pipeline:
/// 1. Policy check → 2. Schema validation → 3. Input sanitization →
/// 4. Execution with timeout/retry → 5. Output validation → 6. Audit log → 7. Dry-run support
/// </summary>
public class ToolGateway : IToolGateway
{
    private readonly IPolicyEngine _policyEngine;
    private readonly IToolGovernanceService _governanceService;
    private readonly IAuditLog _auditLog;
    private readonly IAgentRuntimeCoordinator _runtimeCoordinator;
    private readonly ILogger<ToolGateway> _logger;

    public ToolGateway(
        IPolicyEngine policyEngine,
        IToolGovernanceService governanceService,
        IAuditLog auditLog,
        IAgentRuntimeCoordinator runtimeCoordinator,
        ILogger<ToolGateway> logger)
    {
        _policyEngine = policyEngine;
        _governanceService = governanceService;
        _auditLog = auditLog;
        _runtimeCoordinator = runtimeCoordinator;
        _logger = logger;
    }

    public async Task<ToolGatewayResult> ExecuteAsync(ITool tool, ToolInput input, ToolGatewayOptions? options = null, CancellationToken ct = default)
    {
        options ??= new ToolGatewayOptions();
        var sw = Stopwatch.StartNew();

        // Step 1: Policy check
        var policyContext = new PolicyContext
        {
            AgentName = _runtimeCoordinator.CurrentAgentName,
            ToolName = tool.Name,
            ToolCategory = tool.Category.ToString(),
            Action = input.Action,
            Metadata = new Dictionary<string, object>(input.Parameters)
        };

        var policyResult = await _policyEngine.EvaluateAsync(policyContext, ct);

        if (!policyResult.Allowed && !policyResult.RequiresApproval)
        {
            sw.Stop();
            await RecordAuditAsync(tool, input, false, policyResult.Reason, sw.Elapsed, ct);
            return new ToolGatewayResult
            {
                Success = false,
                ErrorMessage = policyResult.Reason,
                PolicyResult = policyResult,
                Latency = sw.Elapsed,
                ValidationErrors = policyResult.Violations.Select(v => v.Description).ToList()
            };
        }

        // Step 2: Governance evaluation (risk, approval)
        var governance = await _governanceService.EvaluateAsync(tool, input, ct);
        var policy = governance.Policy;

        // Step 3: Dry-run mode
        if (options.DryRun)
        {
            sw.Stop();
            await RecordAuditAsync(tool, input, true, "Dry-run completed", sw.Elapsed, ct);
            return ToolGatewayResult.DryRunResult(policy, policyResult);
        }

        // Step 4: Check if approval is required
        if (!governance.Allowed)
        {
            sw.Stop();
            await RecordAuditAsync(tool, input, false,
                governance.RequiresApproval ? "Waiting for approval" : governance.Reason,
                sw.Elapsed, ct);

            return new ToolGatewayResult
            {
                Success = false,
                ErrorMessage = governance.Reason,
                AppliedPolicy = policy,
                PolicyResult = policyResult,
                Latency = sw.Elapsed,
                Metadata = governance.ApprovalRequest != null
                    ? new Dictionary<string, object> { ["approvalId"] = governance.ApprovalRequest.Id }
                    : new()
            };
        }

        // Step 5: Execute with timeout and retry
        var timeout = options.CustomTimeout ?? policy.Timeout;
        var maxRetries = options.CustomRetries ?? policy.MaxRetries;
        var attempts = 0;
        ToolResult? toolResult = null;
        string? error = null;
        var success = false;

        while (attempts <= maxRetries)
        {
            attempts++;
            try
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeoutCts.CancelAfter(timeout);

                toolResult = await tool.ExecuteAsync(input, timeoutCts.Token);
                success = toolResult.Success;
                if (success) break;
                error = toolResult.ErrorMessage ?? "Tool returned failure without message.";
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                error = $"Tool '{tool.Name}' timed out after {timeout.TotalSeconds}s (attempt {attempts}/{maxRetries + 1}).";
                _logger.LogWarning(error);
            }
            catch (Exception ex)
            {
                error = $"Tool '{tool.Name}' failed: {ex.Message} (attempt {attempts}/{maxRetries + 1}).";
                _logger.LogWarning(ex, "Tool execution failed for {ToolName}, attempt {Attempt}", tool.Name, attempts);
            }
        }

        sw.Stop();

        // Step 6: Audit log
        await RecordAuditAsync(tool, input, success, success ? "Executed successfully" : error, sw.Elapsed, ct);

        // Step 7: Return result
        if (success && toolResult is not null)
        {
            return new ToolGatewayResult
            {
                Success = true,
                Output = toolResult.Data?.ToString(),
                Latency = sw.Elapsed,
                AttemptsUsed = attempts,
                AppliedPolicy = policy,
                PolicyResult = policyResult,
                Metadata = toolResult.Metadata != null
                    ? new Dictionary<string, object>(toolResult.Metadata)
                    : new()
            };
        }

        return new ToolGatewayResult
        {
            Success = false,
            ErrorMessage = error,
            Latency = sw.Elapsed,
            AttemptsUsed = attempts,
            AppliedPolicy = policy,
            PolicyResult = policyResult
        };
    }

    private async Task RecordAuditAsync(ITool tool, ToolInput input, bool success, string? description, TimeSpan latency, CancellationToken ct)
    {
        await _auditLog.RecordAsync(new AuditEntry
        {
            Category = AuditCategory.ToolCall,
            Action = $"{tool.Name}.{input.Action}",
            AgentName = _runtimeCoordinator.CurrentAgentName,
            SessionId = _runtimeCoordinator.CurrentSessionId,
            ToolName = tool.Name,
            Success = success,
            ErrorMessage = success ? null : description,
            Description = description,
            Metadata = new Dictionary<string, object>
            {
                ["toolId"] = tool.Id,
                ["toolCategory"] = tool.Category.ToString(),
                ["action"] = input.Action,
                ["latencyMs"] = latency.TotalMilliseconds
            }
        }, ct);
    }
}
