using FluentAssertions;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Skills;

namespace AgenticSystem.Tests;

public class BuiltInSkillsTests
{
    [Fact]
    public async Task CodingAssistantSkill_GetContent_ReturnsPromptFragment()
    {
        var skill = new CodingAssistantSkill();
        var context = new SkillContext { AgentName = "WorkAgent", Domain = "work" };

        var content = await skill.GetContentAsync(context);

        content.SystemPromptFragment.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void CodingAssistantSkill_Properties_AreCorrect()
    {
        var skill = new CodingAssistantSkill();
        skill.Id.Should().NotBeNullOrWhiteSpace();
        skill.Domain.Should().Be("work");
        skill.Type.Should().Be(SkillType.Instruction);
    }

    [Fact]
    public async Task ProductivitySkill_GetContent_ReturnsPromptFragment()
    {
        var skill = new ProductivitySkill();
        var context = new SkillContext { AgentName = "PersonalAgent", Domain = "personal" };

        var content = await skill.GetContentAsync(context);

        content.SystemPromptFragment.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task CreativeWritingSkill_GetContent_ReturnsPromptFragment()
    {
        var skill = new CreativeWritingSkill();
        var context = new SkillContext { AgentName = "CreativeAgent", Domain = "creative" };

        var content = await skill.GetContentAsync(context);

        content.SystemPromptFragment.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task DataAnalysisSkill_GetContent_ReturnsPromptFragment()
    {
        var skill = new DataAnalysisSkill();
        var context = new SkillContext { AgentName = "AnalysisAgent", Domain = "analysis" };

        var content = await skill.GetContentAsync(context);

        content.SystemPromptFragment.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void DataAnalysisSkill_IsKnowledgeType()
    {
        var skill = new DataAnalysisSkill();
        skill.Type.Should().Be(SkillType.Knowledge);
    }
}
