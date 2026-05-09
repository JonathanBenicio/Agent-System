using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Core.LLM.Interfaces;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Core.Services;

public class AdaptiveModelRouter : IModelRouter
{
    private readonly IModelPerformanceStore _performanceStore;
    private readonly ILLMAdministrationService _llmAdmin;
    private readonly ILogger<AdaptiveModelRouter> _logger;

    public AdaptiveModelRouter(
        IModelPerformanceStore performanceStore,
        ILLMAdministrationService llmAdmin,
        ILogger<AdaptiveModelRouter> logger)
    {
        _performanceStore = performanceStore;
        _llmAdmin = llmAdmin;
        _logger = logger;
    }

    public async Task<ModelRoutingDecision> RouteToModelAsync(ModelRoutingRequest request, CancellationToken ct = default)
    {
        _logger.LogInformation("🎯 Routing task: {Description} (Priority={Priority})", 
            request.TaskDescription.Length > 50 ? request.TaskDescription[..50] + "..." : request.TaskDescription, 
            request.Priority);

        var models = await GetAvailableModelsAsync(ct);
        var aggregatedMetrics = await _performanceStore.GetAggregatedMetricsAsync(ct);

        // 1. Filter models by required capabilities
        var candidates = models.Where(m => 
            m.IsAvailable &&
            (!request.RequiresJsonMode || m.SupportsJsonMode) &&
            (!request.RequiresVision || m.SupportsVision) &&
            (!request.RequiresFunctionCalling || m.SupportsFunctionCalling) &&
            (request.PreferredProvider == null || m.Provider.Equals(request.PreferredProvider, StringComparison.OrdinalIgnoreCase)) &&
            (request.PreferredRegion == null || m.Region == null || m.Region.Equals(request.PreferredRegion, StringComparison.OrdinalIgnoreCase))
        ).ToList();

        if (candidates.Count == 0)
        {
            throw new InvalidOperationException("No models found satisfying the required capabilities.");
        }

        // 2. Score candidates based on priority and performance
        var scoredCandidates = candidates.Select(m => 
        {
            double baseScore = 0;
            
            // Adjust metrics with real-world performance if available
            var latency = aggregatedMetrics.TryGetValue(m.ModelId, out var perf) ? perf.AverageLatencyMs : m.AverageLatencyMs;
            var successRate = aggregatedMetrics.TryGetValue(m.ModelId, out var p) ? p.SuccessRate : 1.0;
            var cost = m.InputCostPer1kTokens + m.OutputCostPer1kTokens;

            switch (request.Priority)
            {
                case ModelRoutingPriority.Quality:
                    baseScore = (m.QualityScore * 0.8) + (successRate * 0.2);
                    break;
                case ModelRoutingPriority.Speed:
                    // Higher score for lower latency (normalized against 5000ms max)
                    var speedScore = Math.Max(0, 1.0 - (latency / 5000.0));
                    baseScore = (speedScore * 0.7) + (successRate * 0.3);
                    break;
                case ModelRoutingPriority.Cost:
                    // Higher score for lower cost (normalized against $0.10 max per 1k)
                    var costScore = Math.Max(0, 1.0 - (cost / 0.10));
                    baseScore = (costScore * 0.8) + (successRate * 0.2);
                    break;
                case ModelRoutingPriority.Balanced:
                default:
                    var balSpeed = Math.Max(0, 1.0 - (latency / 5000.0));
                    var balCost = Math.Max(0, 1.0 - (cost / 0.10));
                    baseScore = (m.QualityScore * 0.4) + (balSpeed * 0.3) + (balCost * 0.3);
                    break;
            }

            // Penalize low success rate heavily
            if (successRate < 0.9) baseScore *= (successRate * successRate);

            return new { Model = m, Score = baseScore, ActualLatency = latency, ActualCost = cost };
        }).OrderByDescending(x => x.Score).ToList();

        var selected = scoredCandidates.First();
        var fallback = scoredCandidates.Skip(1).FirstOrDefault()?.Model;

        _logger.LogInformation("✅ Selected model {ModelId} (Score={Score:F2}, Latency={Latency}ms)", 
            selected.Model.ModelId, selected.Score, (int)selected.ActualLatency);

        return new ModelRoutingDecision
        {
            ModelId = selected.Model.ModelId,
            Provider = selected.Model.Provider,
            RoutingReason = $"Best fit for {request.Priority} priority based on capability and performance.",
            EstimatedCostUsd = selected.ActualCost * (request.EstimatedInputTokens + request.EstimatedOutputTokens) / 1000.0,
            EstimatedLatencyMs = selected.ActualLatency,
            ConfidenceScore = selected.Score,
            FallbackModelId = fallback?.ModelId,
            RoutingMetadata = new Dictionary<string, object>
            {
                ["qualityScore"] = selected.Model.QualityScore,
                ["successRate"] = aggregatedMetrics.TryGetValue(selected.Model.ModelId, out var sm) ? sm.SuccessRate : 1.0,
                ["sampleCount"] = aggregatedMetrics.TryGetValue(selected.Model.ModelId, out var sc) ? sc.SampleCount : 0
            }
        };
    }

    public async Task<IReadOnlyList<ModelCapability>> GetAvailableModelsAsync(CancellationToken ct = default)
    {
        var config = await _llmAdmin.GetConfigurationAsync(ct);
        return config.Providers
            .Where(p => p.IsEnabled)
            .Select(p => new ModelCapability
            {
                ModelId = p.DefaultModel,
                Provider = p.Name,
                DisplayName = $"{p.Name} - {p.DefaultModel}",
                MaxContextTokens = 128000, // Default, should be dynamic in a real app
                SupportsJsonMode = true,
                SupportsVision = p.Name.Contains("OpenAI", StringComparison.OrdinalIgnoreCase) || p.Name.Contains("Gemini", StringComparison.OrdinalIgnoreCase),
                SupportsFunctionCalling = true,
                SupportsStreaming = true,
                InputCostPer1kTokens = EstimateCost(p.Name, true),
                OutputCostPer1kTokens = EstimateCost(p.Name, false),
                AverageLatencyMs = 1500.0, // Initial estimate
                QualityScore = p.Priority == 1 ? 0.9 : 0.7,
                IsAvailable = p.IsAvailable
            }).ToList();
    }

    public Task RecordModelPerformanceAsync(string modelId, ModelPerformanceRecord record, CancellationToken ct = default)
        => _performanceStore.RecordPerformanceAsync(modelId, record, ct);

    private double EstimateCost(string provider, bool input)
    {
        if (provider.Contains("OpenAI", StringComparison.OrdinalIgnoreCase)) return input ? 0.005 : 0.015;
        if (provider.Contains("Gemini", StringComparison.OrdinalIgnoreCase)) return input ? 0.00125 : 0.00375;
        if (provider.Contains("Claude", StringComparison.OrdinalIgnoreCase)) return input ? 0.003 : 0.015;
        return 0.0; // Ollama/Local
    }
}
