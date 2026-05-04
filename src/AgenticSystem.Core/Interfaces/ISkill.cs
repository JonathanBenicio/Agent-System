namespace AgenticSystem.Core.Interfaces;

public interface ISkill
{
    string Id { get; }
    string Name { get; }
    string Domain { get; }
    SkillType Type { get; }
    Task<SkillContent> GetContentAsync(SkillContext context);
}

public enum SkillType
{
    Instruction,
    Knowledge,
    Template
}

public record SkillContent
{
    public string SystemPromptFragment { get; init; } = string.Empty;
    public string? FewShotExamples { get; init; }
    public Dictionary<string, string>? Metadata { get; init; }
}

public record SkillContext
{
    public string AgentName { get; init; } = string.Empty;
    public string TaskType { get; init; } = string.Empty;
    public string Domain { get; init; } = string.Empty;
    public Dictionary<string, object> Parameters { get; init; } = new();
}
