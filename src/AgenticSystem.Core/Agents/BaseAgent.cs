using Microsoft.Extensions.Logging;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.LLM.Interfaces;
using AgenticSystem.Core.LLM.Models;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Agents;

public abstract class BaseAgent : IAgent
{
    private readonly ILLMManager _llmManager;
    private readonly ISkillManager _skillManager;
    private readonly ILogger _logger;

    protected BaseAgent(ILLMManager llmManager, ISkillManager skillManager, ILogger logger)
    {
        _llmManager = llmManager;
        _skillManager = skillManager;
        _logger = logger;
        CreatedAt = DateTime.UtcNow;
        LastUsedAt = DateTime.UtcNow;
    }

    public abstract string Name { get; }
    public abstract string Description { get; }
    public abstract AgentTier Tier { get; }
    public abstract string Domain { get; }
    public DateTime CreatedAt { get; }
    public DateTime LastUsedAt { get; private set; }
    public bool IsActive { get; set; } = true;
    public virtual IEnumerable<string> AvailableTools => Enumerable.Empty<string>();

    public async Task<AgentResponse> ExecuteAsync(string input, UserContext context)
    {
        UpdateLastUsed();
        _logger.LogInformation("🤖 [{Agent}] Executando: {Input}", Name, Truncate(input, 80));

        try
        {
            var systemPrompt = await BuildSystemPromptAsync(context);
            var response = await _llmManager.GenerateWithProfileAsync(
                Name, "default", $"{systemPrompt}\n\nUser: {input}");

            if (!response.Success)
            {
                return AgentResponse.Error(response.ErrorMessage ?? "LLM failed", Name);
            }

            var result = await ProcessResponseAsync(response.Content, input, context);
            result.AgentName = Name;
            result.AgentTier = Tier;
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ [{Agent}] Erro: {Message}", Name, ex.Message);
            return AgentResponse.Error(ex.Message, Name);
        }
    }

    public virtual async Task<bool> CanHandleAsync(AnalysisResult analysis)
    {
        var domainMatch = analysis.PrimaryDomain.Equals(Domain, StringComparison.OrdinalIgnoreCase) ||
                          analysis.SecondaryDomains.Contains(Domain, StringComparer.OrdinalIgnoreCase);
        var tierMatch = analysis.RecommendedTier >= Tier;
        return await Task.FromResult(domainMatch && tierMatch);
    }

    public void UpdateLastUsed() => LastUsedAt = DateTime.UtcNow;

    protected virtual async Task<string> BuildSystemPromptAsync(UserContext context)
    {
        var enrichedPrompt = await _skillManager.BuildEnrichedPromptAsync(Name, Domain, GetBaseSystemPrompt());
        return enrichedPrompt;
    }

    protected virtual Task<AgentResponse> ProcessResponseAsync(string llmContent, string userInput, UserContext context)
    {
        return Task.FromResult(AgentResponse.Ok(llmContent, Name, Tier));
    }

    protected abstract string GetBaseSystemPrompt();

    private static string Truncate(string text, int maxLength)
        => text.Length <= maxLength ? text : text[..maxLength] + "...";
}
