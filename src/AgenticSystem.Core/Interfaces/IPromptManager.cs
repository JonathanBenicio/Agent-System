using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Interfaces;

/// <summary>
/// Service for managing prompt templates with versioning and locale support.
/// </summary>
public interface IPromptManager
{
    /// <summary>
    /// Resolves and renders the active prompt for an agent, substituting variables.
    /// </summary>
    Task<string> ResolvePromptAsync(
        string agentName,
        Dictionary<string, string>? variables = null,
        string locale = "pt-BR",
        CancellationToken ct = default);

    /// <summary>
    /// Creates or updates a prompt template.
    /// </summary>
    Task<PromptTemplate> SaveTemplateAsync(
        PromptTemplate template,
        CancellationToken ct = default);

    /// <summary>
    /// Returns all templates for a given agent.
    /// </summary>
    Task<IReadOnlyList<PromptTemplate>> GetTemplatesAsync(
        string agentName,
        CancellationToken ct = default);

    /// <summary>
    /// Returns the active template for an agent in a specific locale.
    /// </summary>
    Task<PromptTemplate?> GetActiveTemplateAsync(
        string agentName,
        string locale = "pt-BR",
        CancellationToken ct = default);
}

/// <summary>
/// Persistence store for prompt templates.
/// </summary>
public interface IPromptTemplateStore
{
    Task SaveAsync(PromptTemplate template, CancellationToken ct = default);
    Task<PromptTemplate?> GetActiveAsync(string agentName, string locale, CancellationToken ct = default);
    Task<IReadOnlyList<PromptTemplate>> GetAllForAgentAsync(string agentName, CancellationToken ct = default);
}
