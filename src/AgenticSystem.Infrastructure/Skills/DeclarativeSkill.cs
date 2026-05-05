using AgenticSystem.Core.Interfaces;

namespace AgenticSystem.Infrastructure.Skills;

internal sealed class DeclarativeSkill : ISkill
{
    private readonly SkillContent _content;

    public DeclarativeSkill(
        string id,
        string name,
        string domain,
        SkillType type,
        SkillContent content)
    {
        Id = id;
        Name = name;
        Domain = domain;
        Type = type;
        _content = content;
    }

    public string Id { get; }
    public string Name { get; }
    public string Domain { get; }
    public SkillType Type { get; }

    public Task<SkillContent> GetContentAsync(SkillContext context)
        => Task.FromResult(_content);
}