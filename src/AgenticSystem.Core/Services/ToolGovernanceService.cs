using System.Collections.Concurrent;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Core.Services;

public class ToolGovernanceService : IToolGovernanceService
{
    private readonly IAgentRuntimeCoordinator _runtimeCoordinator;
    private readonly IPolicyEngine _policyEngine;
    private readonly ILogger<ToolGovernanceService> _logger;
    private readonly ConcurrentDictionary<string, ToolApprovalRequest> _approvals = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, TaskCompletionSource<ToolApprovalRequest>> _approvalWaiters = new(StringComparer.OrdinalIgnoreCase);

    public ToolGovernanceService(IAgentRuntimeCoordinator runtimeCoordinator, IPolicyEngine policyEngine, ILogger<ToolGovernanceService> logger)
    {
        _runtimeCoordinator = runtimeCoordinator;
        _policyEngine = policyEngine;
        _logger = logger;
    }

    public async Task<ToolExecutionDecision> EvaluateAsync(ITool tool, ToolInput input, CancellationToken ct = default)
    {
        var policy = BuildPolicy(tool, input);
        var allowedTools = _runtimeCoordinator.CurrentAllowedTools;

        // Phase 1 — Declarative policy engine evaluation (deny-first)
        var policyContext = new PolicyContext
        {
            AgentName = _runtimeCoordinator.CurrentAgentName,
            ToolName = tool.Name,
            ToolCategory = tool.Category.ToString(),
            Action = input.Action,
            RiskLevel = policy.RiskLevel
        };

        var policyResult = await _policyEngine.EvaluateAsync(policyContext, ct);

        if (!policyResult.Allowed && !policyResult.RequiresApproval)
        {
            _logger.LogWarning("Policy engine denied tool '{ToolName}': {Reason}", tool.Name, policyResult.Reason);
            return new ToolExecutionDecision
            {
                Allowed = false,
                Reason = policyResult.Reason,
                Policy = policy
            };
        }

        if (policyResult.RequiresApproval)
        {
            policy.RequiresApproval = true;
        }

        if (allowedTools.Count > 0 && !IsAllowedForCurrentAgent(tool, allowedTools))
        {
            return new ToolExecutionDecision
            {
                Allowed = false,
                Reason = $"Tool '{tool.Name}' is not allowed for agent '{_runtimeCoordinator.CurrentAgentName ?? "unknown"}'.",
                Policy = policy
            };
        }

        if (policy.RequireIdempotencyKey && !input.Parameters.ContainsKey("idempotencyKey"))
        {
            return new ToolExecutionDecision
            {
                Allowed = false,
                Reason = $"Tool '{tool.Name}' requires an idempotencyKey for write operations.",
                Policy = policy
            };
        }

        if (!policy.RequiresApproval)
        {
            return new ToolExecutionDecision
            {
                Allowed = true,
                Reason = "Tool execution allowed by policy.",
                Policy = policy
            };
        }

        if (input.Parameters.TryGetValue("approvalId", out var rawApprovalId)
            && rawApprovalId is not null
            && _approvals.TryGetValue(rawApprovalId.ToString() ?? string.Empty, out var existingApproval)
            && existingApproval.Status == ToolApprovalStatus.Approved)
        {
            return new ToolExecutionDecision
            {
                Allowed = true,
                Reason = "Tool execution approved by human reviewer.",
                Policy = policy,
                ApprovalRequest = existingApproval
            };
        }

        var approval = new ToolApprovalRequest
        {
            SessionId = _runtimeCoordinator.CurrentSessionId ?? string.Empty,
            ToolId = tool.Id,
            ToolName = tool.Name,
            AgentName = _runtimeCoordinator.CurrentAgentName,
            RiskLevel = policy.RiskLevel,
            Reason = BuildApprovalReason(tool, input, policy),
            RequestedInput = new Dictionary<string, object>(input.Parameters)
        };

        _approvals[approval.Id] = approval;

        await _runtimeCoordinator.RecordArtifactAsync(new AgentExecutionArtifact
        {
            SessionId = approval.SessionId,
            Type = AgentExecutionArtifactType.ToolApproval,
            Name = $"Approval for {tool.Name}",
            AgentName = approval.AgentName,
            Status = approval.Status.ToString(),
            Summary = approval.Reason,
            Data = new Dictionary<string, object>
            {
                ["approvalId"] = approval.Id,
                ["toolId"] = tool.Id,
                ["riskLevel"] = approval.RiskLevel.ToString(),
                ["requestedInput"] = approval.RequestedInput
            }
        }, ct);

        await _runtimeCoordinator.PublishEventAsync(new AgentStreamEvent
        {
            Type = AgentStreamEventType.ToolApprovalRequired,
            AgentName = approval.AgentName,
            Message = approval.Reason,
            Data = new Dictionary<string, object>
            {
                ["approvalId"] = approval.Id,
                ["toolId"] = tool.Id,
                ["riskLevel"] = approval.RiskLevel.ToString()
            }
        }, ct);

        _logger.LogWarning("Tool approval required for {ToolId} in session {SessionId}", tool.Id, approval.SessionId);

        if (_runtimeCoordinator.CurrentStateMachine != null && _runtimeCoordinator.CurrentStateMachine.CanTransition(AgentExecutionState.WaitingHumanApproval))
        {
            await _runtimeCoordinator.TransitionStateAsync(AgentExecutionState.WaitingHumanApproval, $"Tool {tool.Name} requires approval.");
        }

        return new ToolExecutionDecision
        {
            Allowed = false,
            RequiresApproval = true,
            Reason = approval.Reason,
            Policy = policy,
            ApprovalRequest = approval
        };
    }

    public async Task<ToolApprovalRequest> WaitForApprovalAsync(string approvalId, TimeSpan timeout, CancellationToken ct = default)
    {
        if (!_approvals.TryGetValue(approvalId, out var approval))
        {
            throw new ArgumentException($"Approval request {approvalId} not found.");
        }

        if (approval.Status != ToolApprovalStatus.Pending)
        {
            return approval;
        }

        var tcs = _approvalWaiters.GetOrAdd(approvalId, _ => new TaskCompletionSource<ToolApprovalRequest>(TaskCreationOptions.RunContinuationsAsynchronously));

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout);

        try
        {
            await using (cts.Token.Register(() => tcs.TrySetCanceled(cts.Token)))
            {
                return await tcs.Task;
            }
        }
        catch (OperationCanceledException)
        {
            if (!_approvals.TryGetValue(approvalId, out var timedOutApproval)) return approval;

            // Automatically reject if timed out
            if (timedOutApproval.Status == ToolApprovalStatus.Pending)
            {
                await ResolveApprovalAsync(approvalId, ToolApprovalStatus.Rejected, "System", "Approval timed out.");
            }

            return timedOutApproval;
        }
    }

    public Task<IReadOnlyList<ToolApprovalRequest>> GetPendingApprovalsAsync(string? sessionId = null, CancellationToken ct = default)
    {
        var approvals = _approvals.Values
            .Where(approval => approval.Status == ToolApprovalStatus.Pending)
            .Where(approval => string.IsNullOrWhiteSpace(sessionId) || approval.SessionId.Equals(sessionId, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(approval => approval.CreatedAt)
            .ToList();

        return Task.FromResult<IReadOnlyList<ToolApprovalRequest>>(approvals);
    }

    public async Task<ToolApprovalRequest?> ResolveApprovalAsync(
        string approvalId,
        ToolApprovalStatus status,
        string resolvedBy,
        string? comment = null,
        CancellationToken ct = default)
    {
        if (!_approvals.TryGetValue(approvalId, out var approval))
        {
            return null;
        }

        approval.Status = status;
        approval.ResolvedAt = DateTime.UtcNow;
        approval.ResolvedBy = resolvedBy;
        approval.Comment = comment;

        await _runtimeCoordinator.RecordArtifactAsync(new AgentExecutionArtifact
        {
            SessionId = approval.SessionId,
            Type = AgentExecutionArtifactType.ToolApproval,
            Name = $"Approval {approval.ToolName}",
            AgentName = approval.AgentName,
            Status = approval.Status.ToString(),
            Summary = comment,
            Data = new Dictionary<string, object>
            {
                ["approvalId"] = approval.Id,
                ["resolvedBy"] = resolvedBy,
                ["resolution"] = status.ToString()
            }
        }, ct);

        await _runtimeCoordinator.PublishEventAsync(new AgentStreamEvent
        {
            Type = AgentStreamEventType.ToolApprovalResolved,
            AgentName = approval.AgentName,
            Message = $"Approval {approval.Id} {status}",
            Data = new Dictionary<string, object>
            {
                ["approvalId"] = approval.Id,
                ["resolution"] = status.ToString(),
                ["resolvedBy"] = resolvedBy
            }
        }, ct);

        // Resume state machine
        if (_runtimeCoordinator.CurrentStateMachine != null && _runtimeCoordinator.CurrentStateMachine.CanTransition(AgentExecutionState.ExecutingTool))
        {
            var nextState = status == ToolApprovalStatus.Approved ? AgentExecutionState.ExecutingTool : AgentExecutionState.Cancelled;
            await _runtimeCoordinator.TransitionStateAsync(nextState, $"Approval {status} by {resolvedBy}");
        }

        if (_approvalWaiters.TryRemove(approvalId, out var tcs))
        {
            tcs.TrySetResult(approval);
        }

        return approval;
    }

    private static ToolExecutionPolicy BuildPolicy(ITool tool, ToolInput input)
    {
        var action = input.Action.ToLowerInvariant();
        var destructive = IsDestructive(action);
        var remote = tool.Category is ToolCategory.Api or ToolCategory.Database or ToolCategory.Email;
        var readOnly = IsReadOnly(action);

        var risk = destructive
            ? ToolRiskLevel.High
            : remote
                ? ToolRiskLevel.Medium
                : ToolRiskLevel.Low;

        if (tool.Category == ToolCategory.Email && !readOnly)
        {
            risk = ToolRiskLevel.Critical;
        }

        return new ToolExecutionPolicy
        {
            ToolId = tool.Id,
            RiskLevel = risk,
            RequiresApproval = risk >= ToolRiskLevel.High,
            Timeout = tool.Category switch
            {
                ToolCategory.Search => TimeSpan.FromSeconds(15),
                ToolCategory.Api => TimeSpan.FromSeconds(30),
                ToolCategory.Database => TimeSpan.FromSeconds(45),
                _ => TimeSpan.FromSeconds(20)
            },
            MaxRetries = destructive ? 0 : remote ? 2 : 1,
            EnableCache = readOnly && tool.Category is ToolCategory.Search or ToolCategory.Database,
            RequireIdempotencyKey = destructive || action.Contains("send", StringComparison.OrdinalIgnoreCase)
        };
    }

    private static bool IsAllowedForCurrentAgent(ITool tool, IReadOnlyCollection<string> allowedTools)
    {
        var normalized = allowedTools.Select(value => value.Trim().ToLowerInvariant()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var toolCategory = tool.Category.ToString().ToLowerInvariant();
        var toolId = tool.Id.ToLowerInvariant();
        var toolName = tool.Name.ToLowerInvariant();

        return normalized.Contains(toolCategory)
            || normalized.Contains(toolId)
            || normalized.Contains(toolName);
    }

    private static string BuildApprovalReason(ITool tool, ToolInput input, ToolExecutionPolicy policy)
        => $"Tool '{tool.Name}' requires approval because action '{input.Action}' has risk level '{policy.RiskLevel}'.";

    private static bool IsDestructive(string action)
        => action.Contains("delete", StringComparison.OrdinalIgnoreCase)
           || action.Contains("remove", StringComparison.OrdinalIgnoreCase)
           || action.Contains("create", StringComparison.OrdinalIgnoreCase)
           || action.Contains("update", StringComparison.OrdinalIgnoreCase)
           || action.Contains("send", StringComparison.OrdinalIgnoreCase)
           || action.Contains("write", StringComparison.OrdinalIgnoreCase)
           || action.Contains("post", StringComparison.OrdinalIgnoreCase)
           || action.Contains("execute", StringComparison.OrdinalIgnoreCase);

    private static bool IsReadOnly(string action)
        => action.Contains("get", StringComparison.OrdinalIgnoreCase)
           || action.Contains("list", StringComparison.OrdinalIgnoreCase)
           || action.Contains("read", StringComparison.OrdinalIgnoreCase)
           || action.Contains("search", StringComparison.OrdinalIgnoreCase)
           || action.Contains("find", StringComparison.OrdinalIgnoreCase);
}