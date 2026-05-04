using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Interfaces;

/// <summary>
/// Sincronização bidirecional com Obsidian vault.
/// Obsidian é a "fonte de verdade humana" para memória episódica.
/// </summary>
public interface IObsidianSync
{
    /// <summary>
    /// Salva evento de sessão como nota estruturada no Obsidian
    /// </summary>
    Task SaveSessionEventAsync(AgentEvent agentEvent);
    
    /// <summary>
    /// Salva definição de agent criado dinamicamente
    /// </summary>
    Task SaveAgentDefinitionAsync(IAgent agent);
    
    /// <summary>
    /// Busca notas relevantes para contexto
    /// </summary>
    Task<List<ObsidianNote>> GetRelevantNotesAsync(string query);
    
    /// <summary>
    /// Monitora alterações no vault para sincronizar com vector DB
    /// </summary>
    Task StartFileWatcherAsync();
    
    /// <summary>
    /// Indexa todo o vault existente
    /// </summary>
    Task IndexExistingVaultAsync();
}