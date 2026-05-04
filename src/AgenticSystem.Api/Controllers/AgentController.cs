using Microsoft.AspNetCore.Mvc;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AgentController : ControllerBase
{
    private readonly IMetaAgent _metaAgent;
    private readonly IAgentFactory _agentFactory;
    private readonly ISessionManager _sessionManager;
    private readonly IToolManager _toolManager;
    private readonly ISkillManager _skillManager;
    private readonly ILogger<AgentController> _logger;

    public AgentController(
        IMetaAgent metaAgent,
        IAgentFactory agentFactory,
        ISessionManager sessionManager,
        IToolManager toolManager,
        ISkillManager skillManager,
        ILogger<AgentController> logger)
    {
        _metaAgent = metaAgent;
        _agentFactory = agentFactory;
        _sessionManager = sessionManager;
        _toolManager = toolManager;
        _skillManager = skillManager;
        _logger = logger;
    }

    /// <summary>
    /// Lista agents ativos
    /// </summary>
    [HttpGet("agents")]
    public async Task<IActionResult> GetActiveAgents()
    {
        var agents = await _metaAgent.GetActiveAgentsAsync();
        return Ok(agents);
    }

    /// <summary>
    /// Lista agents por tier
    /// </summary>
    [HttpGet("agents/tier/{tier}")]
    public async Task<IActionResult> GetAgentsByTier(AgentTier tier)
    {
        var agents = await _agentFactory.GetAgentsByTierAsync(tier);
        return Ok(agents);
    }

    /// <summary>
    /// Cria agent customizado
    /// </summary>
    [HttpPost("agents")]
    public async Task<IActionResult> CreateAgent([FromBody] AgentSpecification spec)
    {
        var agent = await _agentFactory.CreateCustomAgentAsync(spec);
        return Created($"api/agent/agents/{agent.Name}", new AgentInfo
        {
            Name = agent.Name,
            Description = agent.Description,
            Tier = agent.Tier,
            IsActive = agent.IsActive,
            CreatedAt = agent.CreatedAt
        });
    }

    /// <summary>
    /// Lista todos os agents registrados
    /// </summary>
    [HttpGet("agents/all")]
    public async Task<IActionResult> GetAllAgents()
    {
        var agents = await _agentFactory.GetAllAgentsAsync();
        return Ok(agents);
    }

    /// <summary>
    /// Obtém um agent pelo nome
    /// </summary>
    [HttpGet("agents/{name}")]
    public async Task<IActionResult> GetAgent(string name)
    {
        var agents = await _agentFactory.GetAllAgentsAsync();
        var agent = agents.FirstOrDefault(a => a.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (agent is null)
            return NotFound(new { error = $"Agent '{name}' not found." });

        return Ok(agent);
    }

    /// <summary>
    /// Atualiza agent existente (recria com nova spec)
    /// </summary>
    [HttpPut("agents/{name}")]
    public async Task<IActionResult> UpdateAgent(string name, [FromBody] AgentSpecification spec)
    {
        var agents = await _agentFactory.GetAllAgentsAsync();
        var existing = agents.FirstOrDefault(a => a.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
            return NotFound(new { error = $"Agent '{name}' not found." });

        await _agentFactory.RemoveAgentAsync(name);
        spec.Name = name;
        var agent = await _agentFactory.CreateCustomAgentAsync(spec);

        return Ok(new AgentInfo
        {
            Name = agent.Name,
            Description = agent.Description,
            Tier = agent.Tier,
            IsActive = agent.IsActive,
            CreatedAt = agent.CreatedAt
        });
    }

    /// <summary>
    /// Remove agent pelo nome
    /// </summary>
    [HttpDelete("agents/{name}")]
    public async Task<IActionResult> DeleteAgent(string name)
    {
        var removed = await _agentFactory.RemoveAgentAsync(name);
        if (!removed)
            return NotFound(new { error = $"Agent '{name}' not found." });

        return NoContent();
    }

    /// <summary>
    /// Executa tool por ID
    /// </summary>
    [HttpPost("tools/{toolId}/execute")]
    public async Task<IActionResult> ExecuteTool(string toolId, [FromBody] ToolInput input, CancellationToken ct)
    {
        var result = await _toolManager.ExecuteToolAsync(toolId, input, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Lista tools disponíveis
    /// </summary>
    [HttpGet("tools")]
    public async Task<IActionResult> GetTools([FromQuery] string? category = null)
    {
        var tools = await _toolManager.GetAvailableToolsAsync(category);
        return Ok(tools.Select(t => new
        {
            t.Id,
            t.Name,
            t.Description,
            category = t.Category.ToString(),
            t.RequiresAuth
        }));
    }

    /// <summary>
    /// Obtém tool por ID
    /// </summary>
    [HttpGet("tools/{toolId}")]
    public IActionResult GetTool(string toolId)
    {
        var tool = _toolManager.GetTool(toolId);
        if (tool is null)
            return NotFound(new { error = $"Tool '{toolId}' not found." });

        return Ok(new
        {
            tool.Id,
            tool.Name,
            tool.Description,
            category = tool.Category.ToString(),
            tool.RequiresAuth
        });
    }

    /// <summary>
    /// Remove tool por ID
    /// </summary>
    [HttpDelete("tools/{toolId}")]
    public IActionResult DeleteTool(string toolId)
    {
        var removed = _toolManager.UnregisterTool(toolId);
        if (!removed)
            return NotFound(new { error = $"Tool '{toolId}' not found." });

        return NoContent();
    }

    /// <summary>
    /// Lista todas as skills
    /// </summary>
    [HttpGet("skills/all")]
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

    /// <summary>
    /// Lista skills por domínio
    /// </summary>
    [HttpGet("skills")]
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

    /// <summary>
    /// Remove skill por ID
    /// </summary>
    [HttpDelete("skills/{skillId}")]
    public IActionResult DeleteSkill(string skillId)
    {
        var removed = _skillManager.UnregisterSkill(skillId);
        if (!removed)
            return NotFound(new { error = $"Skill '{skillId}' not found." });

        return NoContent();
    }

    /// <summary>
    /// Lista eventos recentes de uma sessão
    /// </summary>
    [HttpGet("sessions/{sessionId}/events")]
    public async Task<IActionResult> GetSessionEvents(string sessionId, [FromQuery] int count = 20)
    {
        var events = await _sessionManager.GetRecentEventsAsync(sessionId, count);
        return Ok(events);
    }

    /// <summary>
    /// Dispara cleanup de agents inativos
    /// </summary>
    [HttpPost("maintenance/cleanup")]
    public async Task<IActionResult> CleanupInactiveAgents()
    {
        await _metaAgent.CleanupInactiveAgentsAsync();
        return Ok(new { message = "Cleanup executado com sucesso.", timestamp = DateTime.UtcNow });
    }
}
