using System.Collections.Concurrent;
using System.Text.Json;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Core.Services;

public class FinalResponseApprovalService : IFinalResponseApprovalService
{
    private const int MaxStoredResponseLength = 12_000;

    private readonly ConcurrentDictionary<string, FinalResponseApprovalRequest> _approvals = new(StringComparer.OrdinalIgnoreCase);
    private readonly IAgentRuntimeCoordinator _runtimeCoordinator;
    private readonly ISessionManager _sessionManager;
    private readonly ILogger<FinalResponseApprovalService> _logger;

    public FinalResponseApprovalService(
        IAgentRuntimeCoordinator runtimeCoordinator,
        ISessionManager sessionManager,
        ILogger<FinalResponseApprovalService> logger)
    {
        _runtimeCoordinator = runtimeCoordinator;
        _sessionManager = sessionManager;
        _logger = logger;
    }

    public async Task<FinalResponseApprovalDecision> EvaluateAsync(
        string sessionId,
        string input,
        AnalysisResult analysis,
        AgentResponse response,
        CancellationToken ct = default)
    {
        var evaluation = EvaluateRisk(analysis, response);
        if (!evaluation.RequiresApproval)
        {
            return new FinalResponseApprovalDecision
            {
                Allowed = true,
                RequiresApproval = false,
                Reason = "Final response approved automatically."
            };
        }

        var request = new FinalResponseApprovalRequest
        {
            SessionId = sessionId,
            AgentName = response.AgentName,
            RiskLevel = evaluation.RiskLevel,
            Reason = evaluation.Reason,
            UserInput = input,
            ProposedResponse = Truncate(response.Content, MaxStoredResponseLength),
            ResponseMetadata = new Dictionary<string, object>(response.Metadata)
        };

        _approvals[request.Id] = request;

        await _sessionManager.AddEventAsync(sessionId, new AgentEvent
        {
            AgentName = "FinalResponseApproval",
            AgentResponse = request.Reason,
            Context = new Dictionary<string, object>
            {
                ["approvalKind"] = "final-response",
                ["finalApprovalId"] = request.Id,
                ["finalApprovalStatus"] = request.Status.ToString(),
                ["finalApprovalRisk"] = request.RiskLevel.ToString(),
                ["finalApprovalReason"] = request.Reason,
                ["finalApprovalAgent"] = request.AgentName,
                ["finalApprovalUserInput"] = Truncate(input, 2_000),
                ["finalApprovalProposedResponse"] = request.ProposedResponse,
                ["finalApprovalMetadata"] = JsonSerializer.Serialize(request.ResponseMetadata)
            }
        });

        await _runtimeCoordinator.RecordArtifactAsync(new AgentExecutionArtifact
        {
            SessionId = sessionId,
            Type = AgentExecutionArtifactType.FinalApproval,
            Name = $"Final response approval {request.Id}",
            AgentName = request.AgentName,
            Status = request.Status.ToString(),
            Summary = request.Reason,
            Data = new Dictionary<string, object>
            {
                ["approvalId"] = request.Id,
                ["riskLevel"] = request.RiskLevel.ToString(),
                ["reason"] = request.Reason,
                ["responsePreview"] = Truncate(request.ProposedResponse, 400)
            }
        }, ct);

        await _runtimeCoordinator.PublishEventAsync(new AgentStreamEvent
        {
            Type = AgentStreamEventType.FinalApprovalRequired,
            AgentName = request.AgentName,
            Message = request.Reason,
            Data = new Dictionary<string, object>
            {
                ["approvalId"] = request.Id,
                ["riskLevel"] = request.RiskLevel.ToString(),
                ["approvalKind"] = "final-response"
            }
        }, ct);

        _logger.LogWarning(
            "Final response approval required for session {SessionId}, agent {Agent}, approval {ApprovalId}",
            sessionId,
            request.AgentName,
            request.Id);

        return new FinalResponseApprovalDecision
        {
            Allowed = false,
            RequiresApproval = true,
            Reason = request.Reason,
            ApprovalRequest = request
        };
    }

    public async Task<IReadOnlyList<FinalResponseApprovalRequest>> GetPendingApprovalsAsync(string? sessionId = null, CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(sessionId))
        {
            await RestoreSessionApprovalsAsync(sessionId);
        }

        var pending = _approvals.Values
            .Where(approval => approval.Status == FinalResponseApprovalStatus.Pending)
            .Where(approval => string.IsNullOrWhiteSpace(sessionId) || approval.SessionId.Equals(sessionId, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(approval => approval.CreatedAt)
            .ToList();

        return pending;
    }

    public async Task<FinalResponseApprovalRequest?> ResolveApprovalAsync(
        string approvalId,
        FinalResponseApprovalStatus status,
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

        await _sessionManager.AddEventAsync(approval.SessionId, new AgentEvent
        {
            AgentName = "FinalResponseApproval",
            AgentResponse = comment ?? status.ToString(),
            Context = new Dictionary<string, object>
            {
                ["approvalKind"] = "final-response",
                ["finalApprovalId"] = approval.Id,
                ["finalApprovalStatus"] = approval.Status.ToString(),
                ["finalApprovalResolvedBy"] = resolvedBy,
                ["finalApprovalResolutionComment"] = comment ?? string.Empty,
                ["finalApprovalAgent"] = approval.AgentName,
                ["finalApprovalProposedResponse"] = approval.ProposedResponse
            }
        });

        await _runtimeCoordinator.RecordArtifactAsync(new AgentExecutionArtifact
        {
            SessionId = approval.SessionId,
            Type = AgentExecutionArtifactType.FinalApproval,
            Name = $"Final response approval {approval.Id}",
            AgentName = approval.AgentName,
            Status = approval.Status.ToString(),
            Summary = comment,
            Data = new Dictionary<string, object>
            {
                ["approvalId"] = approval.Id,
                ["resolution"] = approval.Status.ToString(),
                ["resolvedBy"] = resolvedBy
            }
        }, ct);

        await _runtimeCoordinator.PublishEventAsync(new AgentStreamEvent
        {
            Type = AgentStreamEventType.FinalApprovalResolved,
            AgentName = approval.AgentName,
            Message = $"Final response approval {approval.Id} {approval.Status}",
            Data = new Dictionary<string, object>
            {
                ["approvalId"] = approval.Id,
                ["resolution"] = approval.Status.ToString(),
                ["resolvedBy"] = resolvedBy,
                ["approvalKind"] = "final-response"
            }
        }, ct);

        return approval;
    }

    private async Task RestoreSessionApprovalsAsync(string? sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return;
        }

        var events = await _sessionManager.GetRecentEventsAsync(sessionId, 500);
        foreach (var ev in events)
        {
            if (!ev.Context.TryGetValue("approvalKind", out var kindObj)
                || !string.Equals(ToStringValue(kindObj), "final-response", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var approvalId = ToStringValue(ev.Context.TryGetValue("finalApprovalId", out var idObj) ? idObj : null);
            if (string.IsNullOrWhiteSpace(approvalId))
            {
                continue;
            }

            if (!_approvals.TryGetValue(approvalId, out var approval))
            {
                approval = new FinalResponseApprovalRequest
                {
                    Id = approvalId,
                    SessionId = sessionId,
                    AgentName = ToStringValue(ev.Context.TryGetValue("finalApprovalAgent", out var agentObj) ? agentObj : null) ?? ev.AgentName,
                    Reason = ToStringValue(ev.Context.TryGetValue("finalApprovalReason", out var reasonObj) ? reasonObj : null) ?? ev.AgentResponse,
                    UserInput = ToStringValue(ev.Context.TryGetValue("finalApprovalUserInput", out var inputObj) ? inputObj : null) ?? string.Empty,
                    ProposedResponse = ToStringValue(ev.Context.TryGetValue("finalApprovalProposedResponse", out var proposedObj) ? proposedObj : null) ?? string.Empty,
                    CreatedAt = ev.Timestamp
                };

                var risk = ToStringValue(ev.Context.TryGetValue("finalApprovalRisk", out var riskObj) ? riskObj : null);
                if (!string.IsNullOrWhiteSpace(risk)
                    && Enum.TryParse<ToolRiskLevel>(risk, true, out var parsedRisk))
                {
                    approval.RiskLevel = parsedRisk;
                }

                _approvals[approval.Id] = approval;
            }

            var statusRaw = ToStringValue(ev.Context.TryGetValue("finalApprovalStatus", out var statusObj) ? statusObj : null);
            if (!string.IsNullOrWhiteSpace(statusRaw)
                && Enum.TryParse<FinalResponseApprovalStatus>(statusRaw, true, out var parsedStatus))
            {
                approval.Status = parsedStatus;
            }

            var resolvedBy = ToStringValue(ev.Context.TryGetValue("finalApprovalResolvedBy", out var resolvedByObj) ? resolvedByObj : null);
            if (!string.IsNullOrWhiteSpace(resolvedBy))
            {
                approval.ResolvedBy = resolvedBy;
                approval.ResolvedAt = ev.Timestamp;
            }

            var comment = ToStringValue(ev.Context.TryGetValue("finalApprovalResolutionComment", out var commentObj) ? commentObj : null);
            if (!string.IsNullOrWhiteSpace(comment))
            {
                approval.Comment = comment;
            }
        }
    }

    private static (bool RequiresApproval, ToolRiskLevel RiskLevel, string Reason) EvaluateRisk(AnalysisResult analysis, AgentResponse response)
    {
        var requiresByMetadata = response.Metadata.TryGetValue("requiresFinalApproval", out var raw)
            && bool.TryParse(raw?.ToString(), out var explicitRequire)
            && explicitRequire;

        var destructiveIntent = analysis.Intent is IntentType.Delete or IntentType.Update;
        var highImpactIntent = analysis.Intent is IntentType.Create or IntentType.Delegate or IntentType.Setup;

        var sensitiveToolUsed = response.ToolsUsed.Any(IsSensitiveToolName);

        if (requiresByMetadata || destructiveIntent || sensitiveToolUsed)
        {
            return (true, ToolRiskLevel.High,
                "A resposta envolve operação sensível e exige aprovação humana antes da publicação.");
        }

        if (highImpactIntent && response.ToolsUsed.Count > 0)
        {
            return (true, ToolRiskLevel.Medium,
                "A resposta utiliza tools com impacto operacional e precisa de revisão humana.");
        }

        return (false, ToolRiskLevel.Low, "No approval required.");
    }

    private static bool IsSensitiveToolName(string toolName)
    {
        var normalized = toolName.ToLowerInvariant();
        return normalized.Contains("delete", StringComparison.OrdinalIgnoreCase)
               || normalized.Contains("remove", StringComparison.OrdinalIgnoreCase)
               || normalized.Contains("update", StringComparison.OrdinalIgnoreCase)
               || normalized.Contains("create", StringComparison.OrdinalIgnoreCase)
               || normalized.Contains("send", StringComparison.OrdinalIgnoreCase)
               || normalized.Contains("payment", StringComparison.OrdinalIgnoreCase)
               || normalized.Contains("checkout", StringComparison.OrdinalIgnoreCase)
               || normalized.Contains("order", StringComparison.OrdinalIgnoreCase)
               || normalized.Contains("deploy", StringComparison.OrdinalIgnoreCase);
    }

    private static string Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.Length <= maxLength
            ? value
            : value[..maxLength] + "...";
    }

    private static string? ToStringValue(object? value)
    {
        return value switch
        {
            null => null,
            string text => text,
            JsonElement element when element.ValueKind == JsonValueKind.String => element.GetString(),
            JsonElement element => element.ToString(),
            _ => value.ToString()
        };
    }
}
