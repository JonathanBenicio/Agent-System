using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace AgenticSystem.Core.Services;

/// <summary>
/// Custom OpenTelemetry instrumentation for the Agentic System.
/// Provides distributed tracing and metrics for agent execution, tool calls, and LLM usage.
/// </summary>
public static class AgenticTelemetry
{
    public const string ServiceName = "AgenticSystem.Runtime";

    // ─── ActivitySource for distributed tracing ───
    public static readonly ActivitySource ActivitySource = new(ServiceName, "1.0.0");

    // ─── Meter for custom metrics ───
    private static readonly Meter Meter = new(ServiceName, "1.0.0");

    // ─── Counters ───
    public static readonly Counter<long> AgentExecutions = Meter.CreateCounter<long>(
        "agentic.agent.executions",
        description: "Total number of agent executions");

    public static readonly Counter<long> ToolCalls = Meter.CreateCounter<long>(
        "agentic.tool.calls",
        description: "Total number of tool calls");

    public static readonly Counter<long> PolicyViolations = Meter.CreateCounter<long>(
        "agentic.policy.violations",
        description: "Total number of policy violations");

    public static readonly Counter<long> ApprovalRequests = Meter.CreateCounter<long>(
        "agentic.approval.requests",
        description: "Total number of human approval requests");

    public static readonly Counter<long> TokensConsumed = Meter.CreateCounter<long>(
        "agentic.llm.tokens",
        unit: "tokens",
        description: "Total tokens consumed across all LLM calls");

    // ─── Histograms ───
    public static readonly Histogram<double> AgentLatency = Meter.CreateHistogram<double>(
        "agentic.agent.latency",
        unit: "ms",
        description: "Agent execution latency in milliseconds");

    public static readonly Histogram<double> ToolLatency = Meter.CreateHistogram<double>(
        "agentic.tool.latency",
        unit: "ms",
        description: "Tool execution latency in milliseconds");

    public static readonly Histogram<double> LlmLatency = Meter.CreateHistogram<double>(
        "agentic.llm.latency",
        unit: "ms",
        description: "LLM call latency in milliseconds");

    public static readonly Histogram<double> LlmCost = Meter.CreateHistogram<double>(
        "agentic.llm.cost",
        unit: "USD",
        description: "LLM call cost in USD");

    // ─── Gauges (via UpDownCounter) ───
    public static readonly UpDownCounter<int> ActiveSessions = Meter.CreateUpDownCounter<int>(
        "agentic.sessions.active",
        description: "Number of active agent sessions");

    public static readonly UpDownCounter<int> PendingApprovals = Meter.CreateUpDownCounter<int>(
        "agentic.approvals.pending",
        description: "Number of pending human approvals");

    // ─── Trace helpers ───
    public static Activity? StartAgentExecution(string agentName, string? sessionId = null)
    {
        var activity = ActivitySource.StartActivity("agent.execute", ActivityKind.Internal);
        activity?.SetTag("agent.name", agentName);
        activity?.SetTag("session.id", sessionId);
        return activity;
    }

    public static Activity? StartToolExecution(string toolName, string action, string? agentName = null)
    {
        var activity = ActivitySource.StartActivity("tool.execute", ActivityKind.Internal);
        activity?.SetTag("tool.name", toolName);
        activity?.SetTag("tool.action", action);
        activity?.SetTag("agent.name", agentName);
        return activity;
    }

    public static Activity? StartLlmCall(string provider, string model, string? agentName = null)
    {
        var activity = ActivitySource.StartActivity("llm.call", ActivityKind.Client);
        activity?.SetTag("llm.provider", provider);
        activity?.SetTag("llm.model", model);
        activity?.SetTag("agent.name", agentName);
        return activity;
    }

    /// <summary>
    /// Records completion metrics for an LLM call.
    /// </summary>
    public static void RecordLlmCompletion(
        string provider,
        string model,
        int inputTokens,
        int outputTokens,
        double costUsd,
        double latencyMs)
    {
        var tags = new TagList
        {
            { "llm.provider", provider },
            { "llm.model", model }
        };

        TokensConsumed.Add(inputTokens + outputTokens, tags);
        LlmCost.Record(costUsd, tags);
        LlmLatency.Record(latencyMs, tags);
    }

    /// <summary>
    /// Records agent execution metrics.
    /// </summary>
    public static void RecordAgentExecution(string agentName, bool success, double latencyMs)
    {
        var tags = new TagList
        {
            { "agent.name", agentName },
            { "agent.success", success }
        };

        AgentExecutions.Add(1, tags);
        AgentLatency.Record(latencyMs, tags);
    }

    /// <summary>
    /// Records tool execution metrics.
    /// </summary>
    public static void RecordToolExecution(string toolName, string action, bool success, double latencyMs)
    {
        var tags = new TagList
        {
            { "tool.name", toolName },
            { "tool.action", action },
            { "tool.success", success }
        };

        ToolCalls.Add(1, tags);
        ToolLatency.Record(latencyMs, tags);
    }
}
