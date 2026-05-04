using Microsoft.Extensions.Logging;
using AgenticSystem.Core.Interfaces;
using System.Collections.Concurrent;

namespace AgenticSystem.Core.Services;

public class InMemorySkillManager : ISkillManager
{
    private readonly ConcurrentDictionary<string, ISkill> _skills = new();
    private readonly ILogger<InMemorySkillManager> _logger;

    public InMemorySkillManager(ILogger<InMemorySkillManager> logger)
    {
        _logger = logger;
    }

    public async Task<IEnumerable<SkillContent>> GetSkillsForAgentAsync(string agentName, string domain)
    {
        var relevantSkills = _skills.Values
            .Where(s => s.Domain.Equals(domain, StringComparison.OrdinalIgnoreCase) ||
                        s.Domain.Equals("general", StringComparison.OrdinalIgnoreCase));

        var contents = new List<SkillContent>();
        foreach (var skill in relevantSkills)
        {
            var context = new SkillContext { AgentName = agentName, Domain = domain };
            contents.Add(await skill.GetContentAsync(context));
        }

        return contents;
    }

    public async Task<string> BuildEnrichedPromptAsync(string agentName, string domain, string basePrompt)
    {
        var skills = await GetSkillsForAgentAsync(agentName, domain);
        var skillFragments = skills
            .Select(s => s.SystemPromptFragment)
            .Where(f => !string.IsNullOrWhiteSpace(f));

        var fragments = string.Join("\n\n", skillFragments);
        return string.IsNullOrWhiteSpace(fragments)
            ? basePrompt
            : $"{basePrompt}\n\n--- Knowledge Context ---\n{fragments}";
    }

    public void RegisterSkill(ISkill skill)
    {
        _skills[skill.Id] = skill;
        _logger.LogInformation("📚 Skill registered: {SkillName} ({Domain})", skill.Name, skill.Domain);
    }

    public bool UnregisterSkill(string skillId)
    {
        var removed = _skills.TryRemove(skillId, out _);
        if (removed)
            _logger.LogInformation("📚 Skill removed: {SkillId}", skillId);
        return removed;
    }

    public IEnumerable<ISkill> GetAllSkills() => _skills.Values;
}
