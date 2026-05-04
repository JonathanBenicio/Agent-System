using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Services;

namespace AgenticSystem.Tests;

public class InMemorySkillManagerTests
{
    private readonly ILogger<InMemorySkillManager> _logger;
    private readonly InMemorySkillManager _sut;

    public InMemorySkillManagerTests()
    {
        _logger = Substitute.For<ILogger<InMemorySkillManager>>();
        _sut = new InMemorySkillManager(_logger);
    }

    [Fact]
    public async Task RegisterSkill_AddsSkillSuccessfully()
    {
        var skill = CreateMockSkill("s1", "Test Skill", "work", SkillType.Instruction);
        _sut.RegisterSkill(skill);

        var skills = await _sut.GetSkillsForAgentAsync("WorkAgent", "work");
        skills.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetSkillsForAgentAsync_FiltersByDomain()
    {
        _sut.RegisterSkill(CreateMockSkill("s1", "Work Skill", "work", SkillType.Instruction));
        _sut.RegisterSkill(CreateMockSkill("s2", "Creative Skill", "creative", SkillType.Knowledge));

        var workSkills = await _sut.GetSkillsForAgentAsync("WorkAgent", "work");
        workSkills.Should().HaveCount(1);
    }

    [Fact]
    public async Task BuildEnrichedPromptAsync_CombinesSkillsWithBase()
    {
        var skill = CreateMockSkill("s1", "Work Skill", "work", SkillType.Instruction);
        skill.GetContentAsync(Arg.Any<SkillContext>())
            .Returns(new SkillContent { SystemPromptFragment = "You are a work assistant." });

        _sut.RegisterSkill(skill);

        var result = await _sut.BuildEnrichedPromptAsync("WorkAgent", "work", "Base prompt.");
        result.Should().Contain("Base prompt.");
    }

    [Fact]
    public async Task GetSkillsForAgentAsync_WithNoMatch_ReturnsEmpty()
    {
        var skills = await _sut.GetSkillsForAgentAsync("NoAgent", "nodomain");
        skills.Should().BeEmpty();
    }

    [Fact]
    public void UnregisterSkill_RemovesExistingSkill()
    {
        _sut.RegisterSkill(CreateMockSkill("s1", "Skill 1", "work", SkillType.Instruction));

        var removed = _sut.UnregisterSkill("s1");

        removed.Should().BeTrue();
        _sut.GetAllSkills().Should().BeEmpty();
    }

    [Fact]
    public void UnregisterSkill_WhenNotExists_ReturnsFalse()
    {
        var removed = _sut.UnregisterSkill("nonexistent");

        removed.Should().BeFalse();
    }

    [Fact]
    public void GetAllSkills_ReturnsAllRegistered()
    {
        _sut.RegisterSkill(CreateMockSkill("s1", "Skill 1", "work", SkillType.Instruction));
        _sut.RegisterSkill(CreateMockSkill("s2", "Skill 2", "creative", SkillType.Knowledge));

        var all = _sut.GetAllSkills();

        all.Should().HaveCount(2);
    }

    private ISkill CreateMockSkill(string id, string name, string domain, SkillType type)
    {
        var skill = Substitute.For<ISkill>();
        skill.Id.Returns(id);
        skill.Name.Returns(name);
        skill.Domain.Returns(domain);
        skill.Type.Returns(type);
        skill.GetContentAsync(Arg.Any<SkillContext>())
            .Returns(new SkillContent { SystemPromptFragment = $"Skill: {name}" });
        return skill;
    }
}
