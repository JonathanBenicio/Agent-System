using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Interfaces;

/// <summary>
/// Gerencia sessões de usuário e consolidação de memória.
/// Implementa conceito "Paperclip-like" - tudo vira evento relacionado.
/// </summary>
public interface ISessionManager
{
    /// <summary>
    /// Inicia nova sessão de usuário
    /// </summary>
    Task<string> StartSessionAsync(UserContext userContext);
    
    /// <summary>
    /// Adiciona evento à sessão atual
    /// </summary>
    Task AddEventAsync(string sessionId, AgentEvent agentEvent);
    
    /// <summary>
    /// Consolida eventos da sessão em memória persistente
    /// </summary>
    Task ConsolidateSessionAsync(string sessionId);
    
    /// <summary>
    /// Obtém histórico recente para contexto
    /// </summary>
    Task<List<AgentEvent>> GetRecentEventsAsync(string sessionId, int count = 10);
    
    /// <summary>
    /// Finaliza sessão e consolida automaticamente
    /// </summary>
    Task EndSessionAsync(string sessionId);
}