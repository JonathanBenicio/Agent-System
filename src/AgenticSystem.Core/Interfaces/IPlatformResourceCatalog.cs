namespace AgenticSystem.Core.Interfaces;

/// <summary>Registers platform-owned tools independently from tenant tool variants.</summary>
public interface IPlatformToolCatalog
{
    void RegisterPlatformTool(ITool tool);
}

/// <summary>Registers platform-owned skills independently from tenant skill records.</summary>
public interface IPlatformSkillCatalog
{
    void RegisterPlatformSkill(ISkill skill);
    bool UnregisterPlatformSkill(string skillId);
}
