using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using AgenticSystem.Core.Interfaces;

namespace AgenticSystem.Infrastructure.AgentFramework;

/// <summary>
/// Provedor oficial de Habilidades do MAF (MessageAIContextProvider).
/// Injeta dinamicamente fragmentos de prompts e instruções das skills relevantes ao domínio do agente no pipeline da LLM.
/// </summary>
public class AgentSkillsProvider : MessageAIContextProvider
{
    private readonly DbAgentSkillsSource _skillsSource;
    private readonly ILogger<AgentSkillsProvider> _logger;
    
    internal const string SkillContextMarker = "[Habilidades do Catálogo de Agente]";

    public AgentSkillsProvider(
        DbAgentSkillsSource skillsSource,
        ILogger<AgentSkillsProvider> logger)
    {
        _skillsSource = skillsSource ?? throw new ArgumentNullException(nameof(skillsSource));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async ValueTask<IEnumerable<ChatMessage>> ProvideMessagesAsync(
        InvokingContext context, CancellationToken ct)
    {
        // Evita injeções redundantes se as skills já foram processadas nesta conversação
        if (context.RequestMessages.Any(m =>
            m.Role == ChatRole.System &&
            m.Text?.Contains(SkillContextMarker, StringComparison.Ordinal) == true))
        {
            return [];
        }

        var agentName = context.Agent?.Name;
        // Obter domínio do agente. O MAF nativamente armazena o domain nas chaves de metadados de Agent.
        var agentDomain = "general";
        if (context.Agent?.Description?.Contains("work", StringComparison.OrdinalIgnoreCase) == true)
        {
            agentDomain = "work";
        }
        else if (context.Agent?.Description?.Contains("personal", StringComparison.OrdinalIgnoreCase) == true)
        {
            agentDomain = "personal";
        }

        try
        {
            var allSkills = await _skillsSource.LoadSkillsAsync(ct);
            var relevantSkills = allSkills
                .Where(s => s.Domain.Equals(agentDomain, StringComparison.OrdinalIgnoreCase) ||
                            s.Domain.Equals("general", StringComparison.OrdinalIgnoreCase));

            var skillFragments = new List<string>();
            foreach (var skill in relevantSkills)
            {
                var skillContext = new SkillContext { AgentName = agentName ?? "Agent", Domain = agentDomain };
                var content = await skill.GetContentAsync(skillContext);
                if (!string.IsNullOrWhiteSpace(content.SystemPromptFragment))
                {
                    skillFragments.Add($"### Skill: {skill.Name}\n{content.SystemPromptFragment}");
                }
            }

            if (skillFragments.Count == 0)
            {
                return [];
            }

            var mergedInstructions = string.Join("\n\n", skillFragments);
            _logger.LogInformation("📚 Injected {Count} skills instructions for agent '{AgentName}' (Domain: {Domain})", 
                skillFragments.Count, agentName, agentDomain);

            return [new ChatMessage(ChatRole.System, $"{SkillContextMarker}\n{mergedInstructions}")];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to inject skill instructions via MessageAIContextProvider, proceeding without skills.");
            return [];
        }
    }
}
