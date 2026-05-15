using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Core.Models.Triage;

namespace AgenticSystem.Core.Services;

/// <summary>
/// ML14 — Smart Router: roteamento inteligente baseado em performance e preferências.
/// </summary>
public class SmartRouter : ISmartRouter
{
    private readonly IUserPreferenceEngine _preferenceEngine;
    private readonly ITriageService _triageService;
    private readonly IEnumerable<IFastPathInterceptor> _fastPathInterceptors;
    private readonly ILogger<SmartRouter> _logger;
    private readonly IExternalQuotaSyncService _quotaService;
    private readonly ConcurrentDictionary<string, object> _metricsLocks = new();
    private readonly ConcurrentDictionary<string, List<AgentPerformanceMetric>> _metrics = new();

    public SmartRouter(
        IUserPreferenceEngine preferenceEngine,
        ITriageService triageService,
        IEnumerable<IFastPathInterceptor> fastPathInterceptors,
        IExternalQuotaSyncService quotaService,
        ILogger<SmartRouter> logger)
    {
        _preferenceEngine = preferenceEngine;
        _triageService = triageService;
        _fastPathInterceptors = fastPathInterceptors;
        _quotaService = quotaService;
        _logger = logger;
    }

    public async Task<(bool IsFastPath, string? Response, QueryTriageResult? Triage)> TriageAsync(string input, UserContext context, CancellationToken ct = default)
    {
        // 1. Camada 0 & 0.5: Fast Path (Heurística e ML Local)

        foreach (var interceptor in _fastPathInterceptors)
        {
            var (isFastPath, response) = await interceptor.EvaluateAsync(input, ct);
            if (isFastPath)
            {
                _logger.LogInformation("🚀 Fast-path triggered via {InterceptorType}", interceptor.GetType().Name);
                return (true, response, null);
            }
        }

        // 2. Camada 1: LLM Triage (gpt-4o-mini)
        _logger.LogDebug("🔍 Initiating LLM Triage for input: {Input}", input.Length > 50 ? input[..50] + "..." : input);
        var triageResult = await _triageService.AnalyzeComplexityAsync(input, ct);

        _logger.LogInformation("🎯 Triage Result: Intent={Intent}, Complexity={Complexity}, RAG={RAG}, Tools={Tools}",
            triageResult.Intent, triageResult.Complexity, triageResult.RequiresRAG, triageResult.RequiresTools);

        return (false, null, triageResult);
    }

    public async Task<RoutingDecision> RouteAsync(AnalysisResult analysis, UserContext context)
    {
        // 1. Check user preferences first
        var preferredAgent = await _preferenceEngine.RecommendAgentAsync(context.UserId, analysis);
        if (!string.IsNullOrWhiteSpace(preferredAgent))
        {
            _logger.LogInformation("🎯 User preference routing → {Agent}", preferredAgent);
            var fallback = await BuildFallbackChainAsync(analysis.PrimaryDomain, preferredAgent);
            return new RoutingDecision
            {
                PrimaryAgent = preferredAgent,
                FallbackChain = fallback,
                RoutingReason = $"User preference: {preferredAgent}",
                ConfidenceScore = 0.9,
                UsedUserPreference = true
            };
        }

        // 2. Performance-based routing
        var rankings = (await GetRankingsByDomainAsync(analysis.PrimaryDomain)).ToList();
        if (rankings.Count > 0)
        {
            var best = rankings.First();
            if (best.TotalRequests >= 3 && best.Score > 0.6)
            {
                _logger.LogInformation("📊 Performance routing → {Agent} (score: {Score:F2})", best.AgentName, best.Score);
                return new RoutingDecision
                {
                    PrimaryAgent = best.AgentName,
                    FallbackChain = rankings.Skip(1).Select(r => r.AgentName).Take(2).ToList(),
                    RoutingReason = $"Performance score: {best.Score:F2} ({best.TotalRequests} requests, {best.SuccessRate:P0} success)",
                    ConfidenceScore = best.Score,
                    UsedUserPreference = false
                };
            }
        }

        // 3. Default: use analysis-based routing
        return new RoutingDecision
        {
            PrimaryAgent = analysis.EstimatedAgent ?? "",
            FallbackChain = [],
            RoutingReason = "Default context-analysis routing",
            ConfidenceScore = analysis.Confidence,
            UsedUserPreference = false
        };
    }

    public Task RecordPerformanceAsync(string agentName, AgentPerformanceMetric metric)
    {
        var agentLock = _metricsLocks.GetOrAdd(agentName, _ => new object());

        _metrics.AddOrUpdate(
            agentName,
            _ =>
            {
                lock (agentLock) { return [metric]; }
            },
            (_, list) =>
            {
                lock (agentLock)
                {
                    list.Add(metric);
                    if (list.Count > 100)
                        list.RemoveRange(0, list.Count - 100);
                    return list;
                }
            });

        _logger.LogDebug("📊 Performance recorded: {Agent} | {Latency}ms | {Success}",
            agentName, metric.Latency.TotalMilliseconds, metric.Success);

        return Task.CompletedTask;
    }

    public Task<IEnumerable<AgentRanking>> GetRankingsByDomainAsync(string domain)
    {
        var rankings = new List<AgentRanking>();

        foreach (var (agentName, metrics) in _metrics)
        {
            var domainMetrics = metrics
                .Where(m => m.Domain.Equals(domain, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (domainMetrics.Count == 0) continue;

            var successRate = domainMetrics.Count(m => m.Success) / (double)domainMetrics.Count;
            var avgLatency = domainMetrics.Average(m => m.Latency.TotalMilliseconds);

            // Score = 0.6 * successRate + 0.3 * (1 - normalizedLatency) + 0.1 * satisfactionAvg
            var normalizedLatency = Math.Min(avgLatency / 5000.0, 1.0); // 5s max
            var satisfactionAvg = domainMetrics
                .Where(m => m.UserSatisfaction.HasValue)
                .Select(m => m.UserSatisfaction!.Value)
                .DefaultIfEmpty(0.5)
                .Average();

            var score = 0.6 * successRate + 0.3 * (1.0 - normalizedLatency) + 0.1 * satisfactionAvg;

            rankings.Add(new AgentRanking
            {
                AgentName = agentName,
                Domain = domain,
                SuccessRate = successRate,
                AverageLatencyMs = avgLatency,
                Score = score,
                TotalRequests = domainMetrics.Count
            });
        }

        var sorted = rankings.OrderByDescending(r => r.Score).AsEnumerable();
        return Task.FromResult(sorted);
    }

    private async Task<List<string>> BuildFallbackChainAsync(string domain, string excludeAgent)
    {
        var rankings = (await GetRankingsByDomainAsync(domain)).ToList();
        return rankings
            .Where(r => !r.AgentName.Equals(excludeAgent, StringComparison.OrdinalIgnoreCase))
            .Take(2)
            .Select(r => r.AgentName)
            .ToList();
    }

    public async Task<ProviderRoutingDecision> RouteProviderAsync(string? requestedProvider, string? requestedModel)
    {
        var primaryProvider = string.IsNullOrWhiteSpace(requestedProvider) ? "OpenAI" : requestedProvider;
        var primaryModel = string.IsNullOrWhiteSpace(requestedModel) ? "gpt-4o-mini" : requestedModel;

        // Check if primary is available
        var isPrimaryAvailable = await _quotaService.IsProviderAvailableAsync(primaryProvider);

        if (isPrimaryAvailable)
        {
            return BuildDecision(primaryProvider, primaryModel);
        }

        _logger.LogWarning("⚠️ Primary provider {Provider} is exhausted. Initiating auto-fallback...", primaryProvider);

        // Try Fallbacks
        var fallbacks = GetFallbackOptions(primaryProvider);
        foreach (var fallback in fallbacks)
        {
            if (await _quotaService.IsProviderAvailableAsync(fallback.Provider))
            {
                _logger.LogInformation("✅ Auto-fallback successful: switching to {Provider} ({Model})", fallback.Provider, fallback.Model);
                return BuildDecision(fallback.Provider, fallback.Model, fallbacks);
            }
        }

        _logger.LogCritical("🚨 ALL PROVIDERS EXHAUSTED! Falling back to primary anyway.");
        return BuildDecision(primaryProvider, primaryModel);
    }

    private ProviderRoutingDecision BuildDecision(string provider, string model, List<ProviderFallbackOption>? existingFallbacks = null)
    {
        var decision = new ProviderRoutingDecision
        {
            PrimaryProvider = provider,
            PrimaryModel = model,
            FallbackChain = existingFallbacks ?? GetFallbackOptions(provider)
        };
        return decision;
    }

    private List<ProviderFallbackOption> GetFallbackOptions(string provider)
    {
        if (provider.Equals("OpenAI", StringComparison.OrdinalIgnoreCase))
        {
            return new List<ProviderFallbackOption>
            {
                new() { Provider = "Gemini", Model = "gemini-1.5-flash" },
                new() { Provider = "Claude", Model = "claude-3-haiku-20240307" }
            };
        }
        
        if (provider.Equals("Gemini", StringComparison.OrdinalIgnoreCase))
        {
            return new List<ProviderFallbackOption>
            {
                new() { Provider = "OpenAI", Model = "gpt-4o-mini" },
                new() { Provider = "Claude", Model = "claude-3-haiku-20240307" }
            };
        }

        return new List<ProviderFallbackOption>
        {
            new() { Provider = "OpenAI", Model = "gpt-4o-mini" },
            new() { Provider = "Gemini", Model = "gemini-1.5-flash" }
        };
    }
}
