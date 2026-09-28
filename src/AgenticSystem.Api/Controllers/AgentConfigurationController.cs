using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AgenticSystem.Core.Interfaces;

namespace AgenticSystem.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/agent")]
public class AgentConfigurationController : ControllerBase
{
    private readonly IAgentConfigurationService _configurationService;
    private readonly IAgentYamlValidator _yamlValidator;
    private readonly IAgentVersioningService? _versioningService;
    private readonly IAgentKnowledgeRoomStore? _agentRoomStore;
    private readonly ITenantContextAccessor _tenantContextAccessor;

    public AgentConfigurationController(
        IAgentConfigurationService configurationService,
        IAgentYamlValidator yamlValidator,
        ITenantContextAccessor tenantContextAccessor,
        IAgentVersioningService? versioningService = null,
        IAgentKnowledgeRoomStore? agentRoomStore = null)
    {
        _configurationService = configurationService;
        _yamlValidator = yamlValidator;
        _tenantContextAccessor = tenantContextAccessor;
        _versioningService = versioningService;
        _agentRoomStore = agentRoomStore;
    }

    public record YamlRequest(string Yaml);

    [HttpPost("agents/validate-yaml")]
    public async Task<IActionResult> ValidateYaml([FromBody] YamlRequest request, CancellationToken ct)
    {
        var result = await _yamlValidator.ValidateAsync(request.Yaml, ct);
        return Ok(result);
    }

    [HttpPost("agents/save-yaml")]
    public async Task<IActionResult> SaveAgentYaml([FromBody] YamlRequest request, CancellationToken ct)
    {
        var result = await _configurationService.SaveAgentFromYamlAsync(request.Yaml, "UI-Editor", ct);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Ok(new { Agent = result.Agent, Version = result.Version });
    }

    [HttpGet("agents/{name}/history")]
    public async Task<IActionResult> GetAgentHistory(string name, [FromQuery] int limit = 20, CancellationToken ct = default)
    {
        if (_versioningService is null)
        {
            return StatusCode(503, new { error = "Serviço de versionamento não disponível." });
        }

        var history = await _versioningService.GetVersionHistoryAsync(name, limit, ct);
        return Ok(history);
    }

    [HttpPost("agents/{name}/rollback/{versionId}")]
    public async Task<IActionResult> RollbackAgent(string name, string versionId, [FromQuery] string rolledBackBy = "UI-Editor", CancellationToken ct = default)
    {
        var result = await _configurationService.RollbackAgentAsync(name, versionId, rolledBackBy, ct);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(new
        {
            Message = result.Message,
            Agent = result.Agent,
            Version = result.Version
        });
    }

    [HttpGet("agents/{name}/rooms")]
    public async Task<IActionResult> GetAgentRooms(string name, CancellationToken ct)
    {
        if (_agentRoomStore is null)
        {
            return StatusCode(503, new { error = "Serviço de associação de salas de conhecimento não disponível." });
        }

        var tenantId = GetTenantId();
        var roomIds = await _agentRoomStore.GetRoomIdsForAgentAsync(name, tenantId, ct);
        return Ok(roomIds);
    }

    [HttpPut("agents/{name}/rooms")]
    [HttpPost("agents/{name}/rooms")]
    public async Task<IActionResult> SetAgentRooms(string name, [FromBody] System.Collections.Generic.List<string> roomIds, CancellationToken ct)
    {
        if (_agentRoomStore is null)
        {
            return StatusCode(503, new { error = "Serviço de associação de salas de conhecimento não disponível." });
        }

        var tenantId = GetTenantId();
        await _agentRoomStore.SetRoomsForAgentAsync(name, roomIds, tenantId, ct);
        return NoContent();
    }

    private string GetTenantId() => _tenantContextAccessor.CurrentTenantId;
}
