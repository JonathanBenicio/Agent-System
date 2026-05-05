using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Interfaces;

/// <summary>
/// Vector database para busca semântica (RAG).
/// Suporta múltiplos índices por tipo de conteúdo.
/// </summary>
public interface IVectorStore
{
    /// <summary>
    /// Insere ou atualiza documento com embedding
    /// </summary>
    Task UpsertAsync(EmbeddingDocument document);

    /// <summary>
    /// Remove um documento pelo id, opcionalmente restringindo à coleção informada.
    /// </summary>
    Task DeleteAsync(string id, string? collection = null);
    
    /// <summary>
    /// Busca semântica por similaridade
    /// </summary>
    Task<SearchResult> SearchAsync(string query, SearchScope scope = SearchScope.All, int maxResults = 10);
    
    /// <summary>
    /// Busca com filtros adicionais
    /// </summary>
    Task<SearchResult> SearchWithFiltersAsync(string query, Dictionary<string, string> filters);
    
    /// <summary>
    /// Lista coleções disponíveis
    /// </summary>
    Task<IEnumerable<string>> GetCollectionsAsync();
    
    /// <summary>
    /// Remove documentos antigos (limpeza)
    /// </summary>
    Task CleanupOldDocumentsAsync(TimeSpan olderThan);
}