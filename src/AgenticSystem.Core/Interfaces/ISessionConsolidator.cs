using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Interfaces;

/// <summary>
/// ML13 — Consolida sessões em resumos via LLM.
/// Extrai fatos, decisões e preferências para memória de longo prazo.
/// </summary>
public interface ISessionConsolidator
{
    /// <summary>
    /// Gera resumo de uma sessão usando LLM
    /// </summary>
    Task<SessionSummary> SummarizeSessionAsync(string sessionId, List<AgentEvent> events);

    /// <summary>
    /// Extrai fatos/decisões/preferências de uma sessão
    /// </summary>
    Task<SessionInsights> ExtractInsightsAsync(string sessionId, List<AgentEvent> events);

    /// <summary>
    /// Busca resumos anteriores relevantes ao contexto atual
    /// </summary>
    Task<IEnumerable<SessionSummary>> GetRelevantSummariesAsync(string query, int maxResults = 5);
}

/// <summary>
/// Resumo consolidado de uma sessão
/// </summary>
public class SessionSummary
{
    public string SessionId { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public List<string> TopicsDiscussed { get; set; } = [];
    public List<string> AgentsUsed { get; set; } = [];
    public int EventCount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public TimeSpan? SessionDuration { get; set; }
}

/// <summary>
/// Insights extraídos de uma sessão
/// </summary>
public class SessionInsights
{
    public string SessionId { get; set; } = string.Empty;
    public List<string> Facts { get; set; } = [];
    public List<string> Decisions { get; set; } = [];
    public List<string> Preferences { get; set; } = [];
    public List<string> ActionItems { get; set; } = [];
}
