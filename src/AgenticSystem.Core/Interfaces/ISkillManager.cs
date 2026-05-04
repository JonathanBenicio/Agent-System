namespace AgenticSystem.Core.Interfaces;

public interface ISkillManager
{
    Task<IEnumerable<SkillContent>> GetSkillsForAgentAsync(string agentName, string domain);
    Task<string> BuildEnrichedPromptAsync(string agentName, string domain, string basePrompt);
    void RegisterSkill(ISkill skill);
    bool UnregisterSkill(string skillId);
    IEnumerable<ISkill> GetAllSkills();
}
