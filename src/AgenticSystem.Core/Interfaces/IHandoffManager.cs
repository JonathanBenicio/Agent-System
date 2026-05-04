using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Interfaces;

/// <summary>
/// ML12 — Gerencia handoffs entre agents durante uma conversa.
/// Quando uma requisição cruza domínios, orquestra delegação e agregação de respostas.
/// </summary>
public interface IHandoffManager
{
    /// <summary>
    /// Avalia se o request precisa de handoff para outro(s) agent(s)
    /// </summary>
    Task<HandoffDecision> EvaluateHandoffAsync(AnalysisResult analysis, IAgent currentAgent);

    /// <summary>
    /// Executa handoff: delega partes da requisição para agents especializados
    /// </summary>
    Task<AgentResponse> ExecuteHandoffAsync(string input, UserContext context, HandoffDecision decision);

    /// <summary>
    /// Registra histórico de handoffs da sessão
    /// </summary>
    Task RecordHandoffAsync(string sessionId, HandoffRecord record);

    /// <summary>
    /// Obtém histórico de handoffs de uma sessão
    /// </summary>
    Task<IEnumerable<HandoffRecord>> GetHandoffHistoryAsync(string sessionId);
}

/// <summary>
/// Decisão sobre se/como fazer handoff
/// </summary>
public class HandoffDecision
{
    public bool ShouldHandoff { get; set; }
    public HandoffStrategy Strategy { get; set; }
    public List<HandoffTarget> Targets { get; set; } = [];
    public string Reason { get; set; } = string.Empty;
}

public enum HandoffStrategy
{
    /// <summary>Não precisa de handoff</summary>
    None,
    /// <summary>Delega para um único agent</summary>
    SingleDelegate,
    /// <summary>Divide entre múltiplos agents e agrega resultados</summary>
    FanOut,
    /// <summary>Encadeia agents em sequência</summary>
    Chain
}

public class HandoffTarget
{
    public string AgentName { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public string SubTask { get; set; } = string.Empty;
    public int Order { get; set; }
}

public class HandoffRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string SessionId { get; set; } = string.Empty;
    public string SourceAgent { get; set; } = string.Empty;
    public string TargetAgent { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public HandoffStrategy Strategy { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public bool Success { get; set; }
}
