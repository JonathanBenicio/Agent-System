using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Interfaces;

/// <summary>
/// ML14 — Smart Routing: roteamento baseado em performance + preferências do usuário.
/// </summary>
public interface ISmartRouter
{
    /// <summary>
    /// Resolve o melhor agent para um request, considerando:
    /// - Performance histórica (latência, taxa de sucesso)
    /// - Preferências do usuário (UserPreferenceEngine)
    /// - Fallback chain se o agent primário falhar
    /// </summary>
    Task<RoutingDecision> RouteAsync(AnalysisResult analysis, UserContext context);

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
