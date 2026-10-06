using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using System.Linq;
using System.Threading.Tasks;

namespace AgenticSystem.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/agent")]
public class AgentManagementController : ControllerBase
{
    private readonly IMetaAgent _metaAgent;
    private readonly IAgentFactory _agentFactory;
    private readonly ITenantContextAccessor _tenantContextAccessor;
    private readonly ITenantIsolationEnforcer _tenantIsolationEnforcer;

    public AgentManagementController(
        IMetaAgent metaAgent,
        IAgentFactory agentFactory,
        ITenantContextAccessor tenantContextAccessor,
        ITenantIsolationEnforcer tenantIsolationEnforcer)
    {
        _metaAgent = metaAgent;
        _agentFactory = agentFactory;
        _tenantContextAccessor = tenantContextAccessor;
        _tenantIsolationEnforcer = tenantIsolationEnforcer;
    }

    [HttpGet("agents")]
    public async Task<IActionResult> GetActiveAgents()
    {
        var agents = await _metaAgent.GetActiveAgentsAsync();
        return Ok(agents);
    }

    [HttpGet("agents/tier/{tier}")]
    public async Task<IActionResult> GetAgentsByTier(AgentTier tier)
    {
        var agents = await _agentFactory.GetAgentsByTierAsync(tier);
        return Ok(agents);
    }

    [HttpPost("agents")]
    public async Task<IActionResult> CreateAgent([FromBody] AgentSpecification spec)
    {
        if (!await _tenantIsolationEnforcer.CanCreateAgentAsync(_tenantContextAccessor.CurrentTenantId, spec.Name))
            return Conflict(new { error = "O limite de agentes deste tenant foi atingido." });

        var agent = await _agentFactory.CreateCustomAgentAsync(spec);
        return Created($"api/agent/agents/{agent.Name}", new AgentInfo
        {
            Name = agent.Name,
            Description = agent.Description,
            Tier = agent.Tier,
            Domain = agent.Domain,
            Instructions = agent.Instructions,
            Capabilities = spec.Capabilities.ToList(),
            Configuration = new(spec.Configuration),
            AvailableTools = agent.AvailableTools.ToList(),
            AutonomyLevel = agent.AutonomyLevel,
            IsActive = agent.IsActive,
            CreatedAt = agent.CreatedAt
        });
    }

    [HttpGet("agents/all")]
    public async Task<IActionResult> GetAllAgents()
    {
        var agents = await _agentFactory.GetAllAgentsAsync();
        return Ok(agents);
    }

    [HttpGet("agents/{name}")]
    public async Task<IActionResult> GetAgent(string name)
    {
        var agents = await _agentFactory.GetAllAgentsAsync();
        var agent = agents.FirstOrDefault(a => a.Name.Equals(name, System.StringComparison.OrdinalIgnoreCase));
        if (agent is null)
            return NotFound(new { error = $"Agent '{name}' not found." });

        return Ok(agent);
    }

    [HttpPut("agents/{name}")]
    public async Task<IActionResult> UpdateAgent(string name, [FromBody] AgentSpecification spec)
    {
        var agents = await _agentFactory.GetAllAgentsAsync();
        var existing = agents.FirstOrDefault(a => a.Name.Equals(name, System.StringComparison.OrdinalIgnoreCase));
        if (existing is null)
            return NotFound(new { error = $"Agent '{name}' not found." });

        if (!await _tenantIsolationEnforcer.CanCreateAgentAsync(_tenantContextAccessor.CurrentTenantId, name))
            return Conflict(new { error = "O limite de agentes deste tenant foi atingido." });

        await _agentFactory.RemoveAgentAsync(name);
        spec.Name = name;
        var agent = await _agentFactory.CreateCustomAgentAsync(spec);

        return Ok(new AgentInfo
        {
            Name = agent.Name,
            Description = agent.Description,
            Tier = agent.Tier,
            Domain = agent.Domain,
            Instructions = agent.Instructions,
            Capabilities = spec.Capabilities.ToList(),
            Configuration = new(spec.Configuration),
            AvailableTools = agent.AvailableTools.ToList(),
            AutonomyLevel = agent.AutonomyLevel,
            IsActive = agent.IsActive,
            CreatedAt = agent.CreatedAt
        });
    }

    [HttpDelete("agents/{name}")]
    public async Task<IActionResult> DeleteAgent(string name)
    {
        var removed = await _agentFactory.RemoveAgentAsync(name);
        if (!removed)
            return NotFound(new { error = $"Agent '{name}' not found." });

        return NoContent();
    }

    [HttpPost("maintenance/cleanup")]
    public async Task<IActionResult> CleanupInactiveAgents()
    {
        await _metaAgent.CleanupInactiveAgentsAsync();
        return Ok(new { message = "Cleanup executado com sucesso.", timestamp = System.DateTime.UtcNow });
    }
}
