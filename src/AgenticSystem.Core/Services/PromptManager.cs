using System.Collections.Concurrent;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Core.Services;

/// <summary>
/// Manages prompt templates with versioning, locale support, and variable substitution.
/// Falls back to agent's built-in Instructions if no template is found.
/// </summary>
public class PromptManager : IPromptManager
{
    private readonly IPromptTemplateStore _store;
    private readonly Func<string, IAgent?> _agentResolver;
    private readonly ILogger<PromptManager> _logger;

    public PromptManager(
        IPromptTemplateStore store,
        Func<string, IAgent?> agentResolver,
        ILogger<PromptManager> logger)
    {
        _store = store;
        _agentResolver = agentResolver;
        _logger = logger;
    }

    public async Task<string> ResolvePromptAsync(
        string agentName,
        Dictionary<string, string>? variables = null,
        string locale = "pt-BR",
        CancellationToken ct = default)
    {
        // Try to find a managed template first
        var template = await _store.GetActiveAsync(agentName, locale, ct);

        if (template != null)
        {
            var rendered = variables != null && variables.Count > 0
                ? template.Render(variables)
                : template.TemplateBody;

            _logger.LogDebug("Resolved prompt template '{Name}' v{Version} for agent {Agent}",
                template.Name, template.Version, agentName);

            return rendered;
        }

        // Fallback to agent's built-in Instructions
        var agent = _agentResolver(agentName);
        if (agent != null)
        {
            _logger.LogDebug("Using built-in Instructions for agent {Agent} (no template found)", agentName);
            return agent.Instructions;
        }

        _logger.LogWarning("No prompt template or agent found for {Agent}", agentName);
        return string.Empty;
    }

    public async Task<PromptTemplate> SaveTemplateAsync(PromptTemplate template, CancellationToken ct = default)
    {
        // Auto-extract variables from the template body
        var extractedVars = PromptTemplate.ExtractVariables(template.TemplateBody);

        var enriched = new PromptTemplate
        {
            Id = template.Id,
            Name = template.Name,
            AgentName = template.AgentName,
            TemplateBody = template.TemplateBody,
            Version = template.Version,
            Locale = template.Locale,
            Variables = extractedVars,
            Description = template.Description,
            IsActive = template.IsActive,
            CreatedBy = template.CreatedBy
        };

        await _store.SaveAsync(enriched, ct);

        _logger.LogInformation("Saved prompt template '{Name}' v{Version} for agent {Agent} ({Locale})",
            enriched.Name, enriched.Version, enriched.AgentName, enriched.Locale);

        return enriched;
    }

    public async Task<IReadOnlyList<PromptTemplate>> GetTemplatesAsync(string agentName, CancellationToken ct = default)
    {
        return await _store.GetAllForAgentAsync(agentName, ct);
    }

    public async Task<PromptTemplate?> GetActiveTemplateAsync(string agentName, string locale = "pt-BR", CancellationToken ct = default)
    {
        return await _store.GetActiveAsync(agentName, locale, ct);
    }
}

/// <summary>
/// In-memory prompt template store for development/testing.
/// </summary>
public class InMemoryPromptTemplateStore : IPromptTemplateStore
{
    private readonly ConcurrentDictionary<string, PromptTemplate> _templates = new(StringComparer.OrdinalIgnoreCase);

    public Task SaveAsync(PromptTemplate template, CancellationToken ct = default)
    {
        _templates[template.Id] = template;
        return Task.CompletedTask;
    }

    public Task<PromptTemplate?> GetActiveAsync(string agentName, string locale, CancellationToken ct = default)
    {
        var active = _templates.Values
            .Where(t => t.AgentName.Equals(agentName, StringComparison.OrdinalIgnoreCase))
            .Where(t => t.Locale.Equals(locale, StringComparison.OrdinalIgnoreCase))
            .Where(t => t.IsActive)
            .OrderByDescending(t => t.Version)
            .FirstOrDefault();

        return Task.FromResult(active);
    }

    public Task<IReadOnlyList<PromptTemplate>> GetAllForAgentAsync(string agentName, CancellationToken ct = default)
    {
        var templates = _templates.Values
            .Where(t => t.AgentName.Equals(agentName, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(t => t.Version)
            .ToList();

        return Task.FromResult<IReadOnlyList<PromptTemplate>>(templates);
    }
}
