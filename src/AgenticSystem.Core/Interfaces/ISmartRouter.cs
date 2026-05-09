using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Interfaces;

/// <summary>
/// ML14 — Smart Routing: roteamento baseado em performance + preferências do usuário.
/// Agora também suporta roteamento e fallback transparente de Provedores LLM (Ex: OpenAI -> Gemini).
/// </summary>
public interface ISmartRouter
{
    /// <summary>
    /// Resolve o melhor agent para um request, considerando performance histórica e preferências.
    /// </summary>
    Task<RoutingDecision> RouteAsync(AnalysisResult analysis, UserContext context);

    /// <summary>
    /// Resolve a cadeia de provedores LLM para execução com fallback (Circuit Breaker).
    /// </summary>
    Task<ProviderRoutingDecision> RouteProviderAsync(string? requestedProvider, string? requestedModel);

    /// <summary>
    /// Registra métricas de performance de um agent
    /// </summary>
    Task RecordPerformanceAsync(string agentName, AgentPerformanceMetric metric);

    /// <summary>
    /// Obtém ranking de agents por domínio
    /// </summary>
    Task<IEnumerable<AgentRanking>> GetRankingsByDomainAsync(string domain);
}

public class RoutingDecision
{
    public string PrimaryAgent { get; set; } = string.Empty;
    public List<string> FallbackChain { get; set; } = [];
    public string RoutingReason { get; set; } = string.Empty;
    public double ConfidenceScore { get; set; }
    public bool UsedUserPreference { get; set; }
}

public class ProviderRoutingDecision
{
    public string PrimaryProvider { get; set; } = string.Empty;
    public string PrimaryModel { get; set; } = string.Empty;
    public List<ProviderFallbackOption> FallbackChain { get; set; } = [];
}

public class ProviderFallbackOption
{
    public string Provider { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
}

public class AgentPerformanceMetric
{
    public TimeSpan Latency { get; set; }
    public bool Success { get; set; }
    public double? UserSatisfaction { get; set; }
    public string Domain { get; set; } = string.Empty;
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
}

public class AgentRanking
{
    public string AgentName { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public double SuccessRate { get; set; }
    public double AverageLatencyMs { get; set; }
    public double Score { get; set; }
    public int TotalRequests { get; set; }
}
