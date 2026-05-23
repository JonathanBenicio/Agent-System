using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AgenticSystem.Core.Interfaces;

namespace AgenticSystem.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/agent")]
public class AgentSkillsController : ControllerBase
{
    private readonly ISkillManager _skillManager;

    public AgentSkillsController(ISkillManager skillManager)
    {
        _skillManager = skillManager;
    }

    [HttpGet("all")]
    public IActionResult GetAllSkills()
    {
        var skills = _skillManager.GetAllSkills()
            .Select(s => new
            {
                s.Id,
                s.Name,
                s.Domain,
                type = s.Type.ToString()
            });

        return Ok(skills);
    }

    [HttpGet]
    public async Task<IActionResult> GetSkills([FromQuery] string? agent = null, [FromQuery] string? domain = null)
    {
        var skills = await _skillManager.GetSkillsForAgentAsync(agent ?? "general", domain ?? "general");
        return Ok(skills.Select(s => new
        {
            systemPrompt = s.SystemPromptFragment,
            examples = s.FewShotExamples,
            metadata = s.Metadata
        }));
    }

    [HttpDelete("{skillId}")]
    public IActionResult DeleteSkill(string skillId)
    {
        var removed = _skillManager.UnregisterSkill(skillId);
        if (!removed)
            return NotFound(new { error = $"Skill '{skillId}' not found." });

        return NoContent();
    }
}
