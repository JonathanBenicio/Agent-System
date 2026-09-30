using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/agent")]
public class AgentRuntimeController : ControllerBase
{
    private readonly IAgentRuntimeCoordinator _runtimeCoordinator;
    private readonly ISessionManager _sessionManager;
    private readonly IToolGovernanceService _toolGovernance;
    private readonly IFinalResponseApprovalService _finalResponseApproval;
    private readonly IOperationalStore? _operationalStore;
    private readonly IRuntimeEvaluator? _runtimeEvaluator;

    public AgentRuntimeController(
        IAgentRuntimeCoordinator runtimeCoordinator,
        ISessionManager sessionManager,
        IToolGovernanceService toolGovernance,
        IFinalResponseApprovalService finalResponseApproval,
        IOperationalStore? operationalStore = null,
        IRuntimeEvaluator? runtimeEvaluator = null)
    {
        _runtimeCoordinator = runtimeCoordinator;
        _sessionManager = sessionManager;
        _toolGovernance = toolGovernance;
        _finalResponseApproval = finalResponseApproval;
        _operationalStore = operationalStore;
        _runtimeEvaluator = runtimeEvaluator;
    }

    [HttpGet("sessions/{sessionId}/events")]
    public async Task<IActionResult> GetSessionEvents(string sessionId, [FromQuery] int count = 20)
    {
        var events = await _sessionManager.GetRecentEventsAsync(sessionId, count);
        return Ok(events);
    }

    [HttpGet("sessions/{sessionId}/artifacts")]
    public async Task<IActionResult> GetSessionArtifacts(string sessionId, CancellationToken ct)
    {
        var artifacts = await _runtimeCoordinator.GetArtifactsAsync(sessionId, ct);
        return Ok(artifacts);
    }

    [HttpGet("sessions/{sessionId}/approvals")]
    public async Task<IActionResult> GetPendingApprovals(string sessionId, CancellationToken ct)
    {
        var approvals = await _toolGovernance.GetPendingApprovalsAsync(sessionId, ct);
        return Ok(approvals);
    }

    [HttpPost("approvals/{approvalId}/approve")]
    public async Task<IActionResult> ApproveToolRequest(string approvalId, [FromBody] ApprovalDecisionRequest request, CancellationToken ct)
    {
        var approval = await _toolGovernance.ResolveApprovalAsync(approvalId, ToolApprovalStatus.Approved, request.DecidedBy, request.Comment, ct);
        if (approval is null)
            return NotFound(new { error = $"Approval '{approvalId}' not found." });

        return Ok(approval);
    }

    [HttpPost("approvals/{approvalId}/reject")]
    public async Task<IActionResult> RejectToolRequest(string approvalId, [FromBody] ApprovalDecisionRequest request, CancellationToken ct)
    {
        var approval = await _toolGovernance.ResolveApprovalAsync(approvalId, ToolApprovalStatus.Rejected, request.DecidedBy, request.Comment, ct);
        if (approval is null)
            return NotFound(new { error = $"Approval '{approvalId}' not found." });

        return Ok(approval);
    }

    [HttpGet("sessions/{sessionId}/final-approvals")]
    public async Task<IActionResult> GetPendingFinalApprovals(string sessionId, CancellationToken ct)
    {
        var approvals = await _finalResponseApproval.GetPendingApprovalsAsync(sessionId, ct);
        return Ok(approvals);
    }

    [HttpPost("final-approvals/{approvalId}/approve")]
    public async Task<IActionResult> ApproveFinalResponse(string approvalId, [FromBody] ApprovalDecisionRequest request, CancellationToken ct)
    {
        var approval = await _finalResponseApproval.ResolveApprovalAsync(approvalId, FinalResponseApprovalStatus.Approved, request.DecidedBy, request.Comment, ct);
        if (approval is null)
            return NotFound(new { error = $"Final approval '{approvalId}' not found." });

        return Ok(approval);
    }

    [HttpPost("final-approvals/{approvalId}/reject")]
    public async Task<IActionResult> RejectFinalResponse(string approvalId, [FromBody] ApprovalDecisionRequest request, CancellationToken ct)
    {
        var approval = await _finalResponseApproval.ResolveApprovalAsync(approvalId, FinalResponseApprovalStatus.Rejected, request.DecidedBy, request.Comment, ct);
        if (approval is null)
            return NotFound(new { error = $"Final approval '{approvalId}' not found." });

        return Ok(approval);
    }

    [HttpGet("runtime/metrics")]
    public async Task<IActionResult> GetRuntimeMetrics([FromQuery] string? sessionId, CancellationToken ct)
    {
        var metrics = await _runtimeCoordinator.GetMetricsAsync(sessionId, ct);
        return Ok(metrics);
    }

    [HttpGet("runtime/artifacts/query")]
    public async Task<IActionResult> QueryArtifacts(
        [FromQuery] string? sessionId,
        [FromQuery] string? type,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int limit = 100,
        CancellationToken ct = default)
    {
        if (_operationalStore is null) return StatusCode(503, new { error = "Operational store não configurado." });

        AgentExecutionArtifactType? artifactType = null;
        if (!string.IsNullOrEmpty(type) && Enum.TryParse<AgentExecutionArtifactType>(type, true, out var parsed))
            artifactType = parsed;

        var artifacts = await _operationalStore.QueryArtifactsAsync(sessionId, artifactType, from, to, limit, ct);
        return Ok(artifacts);
    }

    [HttpGet("runtime/metrics/history")]
    public async Task<IActionResult> GetMetricsHistory(
        [FromQuery] string? sessionId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int limit = 50,
        CancellationToken ct = default)
    {
        if (_operationalStore is null) return StatusCode(503, new { error = "Operational store não configurado." });

        var history = await _operationalStore.GetMetricsHistoryAsync(sessionId, from, to, limit, ct);
        return Ok(history);
    }

    [HttpGet("runtime/evaluations")]
    public async Task<IActionResult> GetEvaluations(
        [FromQuery] string? agentName,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken ct = default)
    {
        if (_operationalStore is null) return StatusCode(503, new { error = "Operational store não configurado." });

        var evaluations = await _operationalStore.GetEvaluationsAsync(sessionId: null, agentName, from, to, ct: ct);
        return Ok(evaluations);
    }

    [HttpGet("runtime/evaluate")]
    public async Task<IActionResult> EvaluateRuntime(
        [FromQuery] string? sessionId,
        [FromQuery] string? agentName,
        CancellationToken ct = default)
    {
        if (_runtimeEvaluator is null) return StatusCode(503, new { error = "Runtime evaluator não configurado." });

        var result = await _runtimeEvaluator.EvaluateAsync(sessionId, agentName, ct);
        return Ok(result);
    }

    [HttpGet("runtime/regressions")]
    public async Task<IActionResult> DetectRegressions(
        [FromQuery] DateTime? since,
        CancellationToken ct = default)
    {
        if (_runtimeEvaluator is null) return StatusCode(503, new { error = "Runtime evaluator não configurado." });

        var regressions = await _runtimeEvaluator.DetectRegressionsAsync(since, ct);
        return Ok(regressions);
    }
}

public record ApprovalDecisionRequest(string DecidedBy, string? Comment = null);