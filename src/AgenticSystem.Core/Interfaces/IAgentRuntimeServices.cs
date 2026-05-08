using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Interfaces;

public interface IAgentRuntimeCoordinator
{
    string? CurrentSessionId { get; }
    UserContext? CurrentUserContext { get; }
    string? CurrentAgentName { get; }
    IReadOnlyCollection<string> CurrentAllowedTools { get; }

    IDisposable BeginExecutionScope(string sessionId, UserContext context);
    IDisposable BeginAgentScope(string agentName, IEnumerable<string>? allowedTools = null);
    IAsyncEnumerable<AgentStreamEvent> StreamAsync(
        string sessionId,
        UserContext context,
        Func<CancellationToken, Task<AgentResponse>> operation,
        CancellationToken ct = default);
    Task PublishEventAsync(AgentStreamEvent streamEvent, CancellationToken ct = default);
    Task RecordArtifactAsync(AgentExecutionArtifact artifact, CancellationToken ct = default);
    Task<IReadOnlyList<AgentExecutionArtifact>> GetArtifactsAsync(string sessionId, CancellationToken ct = default);
    Task<AgentRuntimeMetricsSnapshot> GetMetricsAsync(string? sessionId = null, CancellationToken ct = default);
}

public interface IToolGovernanceService
{
    Task<ToolExecutionDecision> EvaluateAsync(ITool tool, ToolInput input, CancellationToken ct = default);
    Task<IReadOnlyList<ToolApprovalRequest>> GetPendingApprovalsAsync(string? sessionId = null, CancellationToken ct = default);
    Task<ToolApprovalRequest?> ResolveApprovalAsync(
        string approvalId,
        ToolApprovalStatus status,
        string resolvedBy,
        string? comment = null,
        CancellationToken ct = default);
}

public interface IFinalResponseApprovalService
{
    Task<FinalResponseApprovalDecision> EvaluateAsync(
        string sessionId,
        string input,
        AnalysisResult analysis,
        AgentResponse response,
        CancellationToken ct = default);
    Task<IReadOnlyList<FinalResponseApprovalRequest>> GetPendingApprovalsAsync(string? sessionId = null, CancellationToken ct = default);
    Task<FinalResponseApprovalRequest?> ResolveApprovalAsync(
        string approvalId,
        FinalResponseApprovalStatus status,
        string resolvedBy,
        string? comment = null,
        CancellationToken ct = default);
}



public interface IAgentCollaborationWorkflow
{
    Task<bool> ShouldRunAsync(string input, AnalysisResult analysis, CancellationToken ct = default);
    Task<AgentResponse> ExecuteAsync(string sessionId, string input, UserContext context, AnalysisResult analysis, CancellationToken ct = default);
}