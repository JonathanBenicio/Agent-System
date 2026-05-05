namespace AgenticSystem.Infrastructure.Configuration;

public sealed class DynamicSkillsOptions
{
    public bool Enabled { get; set; } = true;
    public string Directory { get; set; } = "skills";
    public bool OverrideExistingSkills { get; set; } = true;
}