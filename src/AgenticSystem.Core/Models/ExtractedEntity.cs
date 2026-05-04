namespace AgenticSystem.Core.Models;

/// <summary>
/// Entidade extraída de uma requisição durante análise de contexto
/// </summary>
public class ExtractedEntity
{
    /// <summary>
    /// Tipo da entidade (person, date, task, project, etc.)
    /// </summary>
    public string Type { get; set; } = string.Empty;
    
    /// <summary>
    /// Valor/nome da entidade
    /// </summary>
    public string Value { get; set; } = string.Empty;
    
    /// <summary>
    /// Confiança da extração (0.0 a 1.0)
    /// </summary>
    public double Confidence { get; set; }
    
    /// <summary>
    /// Posição no texto original
    /// </summary>
    public int StartPosition { get; set; }
    
    /// <summary>
    /// Tamanho no texto original
    /// </summary>
    public int Length { get; set; }
    
    /// <summary>
    /// Contexto adicional da entidade
    /// </summary>
    public Dictionary<string, object> Context { get; set; } = new();
}