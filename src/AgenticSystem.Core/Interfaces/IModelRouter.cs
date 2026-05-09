using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Interfaces;

/// <summary>
/// Advanced model router that extends ISmartRouter with cost/latency/capability-aware routing.
/// Routes requests to the optimal LLM provider based on task requirements.
/// </summary>
public interface IModelRouter
{
    /// <summary>
    /// Routes to the best model considering cost, latency, capabilities, and data sensitivity.
    /// </summary>
    Task<ModelRoutingDecision> RouteToModelAsync(
        ModelRoutingRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Returns all available model configurations.
    /// </summary>
    Task<IReadOnlyList<ModelCapability>> GetAvailableModelsAsync(CancellationToken ct = default);

    /// <summary>
    /// Records model performance for adaptive routing.
    /// </summary>
    Task RecordModelPerformanceAsync(
        string modelId,
        ModelPerformanceRecord record,
        CancellationToken ct = default);
}

/// <summary>
/// Request for model routing.
/// </summary>
public class ModelRoutingRequest
{
    public string TaskDescription { get; init; } = string.Empty;
    public ModelRoutingPriority Priority { get; init; } = ModelRoutingPriority.Balanced;
    public bool RequiresJsonMode { get; init; }
    public bool RequiresVision { get; init; }
    public bool RequiresFunctionCalling { get; init; }
    public bool ContainsSensitiveData { get; init; }
    public int EstimatedInputTokens { get; init; }
    public int EstimatedOutputTokens { get; init; }
    public double? MaxCostUsd { get; init; }
    public double? MaxLatencyMs { get; init; }
    public string? PreferredRegion { get; init; }
    public string? PreferredProvider { get; init; }
}

public enum ModelRoutingPriority
{
    Quality,    // Best output quality, cost secondary
    Speed,      // Lowest latency, quality secondary
    Cost,       // Cheapest option, quality minimum
    Balanced    // Balanced across all dimensions
}

/// <summary>
/// Result of model routing.
/// </summary>
public class ModelRoutingDecision
{
    public string ModelId { get; init; } = string.Empty;
    public string Provider { get; init; } = string.Empty;
    public string RoutingReason { get; init; } = string.Empty;
    public double EstimatedCostUsd { get; init; }
    public double EstimatedLatencyMs { get; init; }
    public double ConfidenceScore { get; init; }
    public string? FallbackModelId { get; init; }
    public Dictionary<string, object> RoutingMetadata { get; init; } = new();
}

/// <summary>
/// Describes a model's capabilities and cost profile.
/// </summary>
public class ModelCapability
{
    public string ModelId { get; init; } = string.Empty;
    public string Provider { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public int MaxContextTokens { get; init; }
    public bool SupportsJsonMode { get; init; }
    public bool SupportsVision { get; init; }
    public bool SupportsFunctionCalling { get; init; }
    public bool SupportsStreaming { get; init; }
    public double InputCostPer1kTokens { get; init; }
    public double OutputCostPer1kTokens { get; init; }
    public double AverageLatencyMs { get; init; }
    public double QualityScore { get; init; } // 0.0 - 1.0 relative quality
    public string? Region { get; init; }
    public bool IsAvailable { get; set; } = true;
}

/// <summary>
/// Performance record for adaptive routing.
/// </summary>
public class ModelPerformanceRecord
{
    public double LatencyMs { get; init; }
    public bool Success { get; init; }
    public int InputTokens { get; init; }
    public int OutputTokens { get; init; }
    public double ActualCostUsd { get; init; }
    public DateTime RecordedAt { get; init; } = DateTime.UtcNow;
}
