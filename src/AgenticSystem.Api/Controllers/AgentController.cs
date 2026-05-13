using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.AgentFramework;

namespace AgenticSystem.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class AgentController : ControllerBase
{
    private readonly IMetaAgent _metaAgent;
    private readonly IAgentFactory _agentFactory;
    private readonly IAgentRuntimeCoordinator _runtimeCoordinator;
    private readonly ISessionManager _sessionManager;
    private readonly IToolManager _toolManager;
    private readonly IToolGovernanceService _toolGovernance;
    private readonly IFinalResponseApprovalService _finalResponseApproval;
    private readonly ISkillManager _skillManager;
    private readonly IOperationalStore? _operationalStore;
    private readonly IRuntimeEvaluator? _runtimeEvaluator;
    private readonly IAgentVersioningService? _versioningService;
    private readonly ILogger<AgentController> _logger;

    public AgentController(
        IMetaAgent metaAgent,
        IAgentFactory agentFactory,
        IAgentRuntimeCoordinator runtimeCoordinator,
        ISessionManager sessionManager,
        IToolManager toolManager,
        IToolGovernanceService toolGovernance,
        IFinalResponseApprovalService finalResponseApproval,
        ISkillManager skillManager,
        ILogger<AgentController> logger,
        IOperationalStore? operationalStore = null,
        IRuntimeEvaluator? runtimeEvaluator = null,
        IAgentVersioningService? versioningService = null)
    {
        _metaAgent = metaAgent;
        _agentFactory = agentFactory;
        _runtimeCoordinator = runtimeCoordinator;
        _sessionManager = sessionManager;
        _toolManager = toolManager;
        _toolGovernance = toolGovernance;
        _finalResponseApproval = finalResponseApproval;
        _skillManager = skillManager;
        _operationalStore = operationalStore;
        _runtimeEvaluator = runtimeEvaluator;
        _versioningService = versioningService;
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

    [HttpGet("sessions/{sessionId}/artifacts")]
    public async Task<IActionResult> GetSessionArtifacts(string sessionId, CancellationToken ct)
    {
        var artifacts = await _runtimeCoordinator.GetArtifactsAsync(sessionId, ct);
        return Ok(artifacts);
    }

    [HttpGet("sessions/{sessionId}/approvals")]
    public async Task<IActionResult> GetPendingApprovals(string sessionId, CancellationToken ct)
    {
        var approvals = await _toolGovernance.GetPendingApprovalsAsync(sessionId, ct);
        return Ok(approvals);
    }

    [HttpPost("approvals/{approvalId}/approve")]
    public async Task<IActionResult> ApproveToolRequest(string approvalId, [FromBody] ApprovalDecisionRequest request, CancellationToken ct)
    {
        var approval = await _toolGovernance.ResolveApprovalAsync(approvalId, ToolApprovalStatus.Approved, request.DecidedBy, request.Comment, ct);
        if (approval is null)
            return NotFound(new { error = $"Approval '{approvalId}' not found." });

        return Ok(approval);
    }

    [HttpPost("approvals/{approvalId}/reject")]
    public async Task<IActionResult> RejectToolRequest(string approvalId, [FromBody] ApprovalDecisionRequest request, CancellationToken ct)
    {
        var approval = await _toolGovernance.ResolveApprovalAsync(approvalId, ToolApprovalStatus.Rejected, request.DecidedBy, request.Comment, ct);
        if (approval is null)
            return NotFound(new { error = $"Approval '{approvalId}' not found." });

        return Ok(approval);
    }

    [HttpGet("sessions/{sessionId}/final-approvals")]
    public async Task<IActionResult> GetPendingFinalApprovals(string sessionId, CancellationToken ct)
    {
        var approvals = await _finalResponseApproval.GetPendingApprovalsAsync(sessionId, ct);
        return Ok(approvals);
    }

    [HttpPost("final-approvals/{approvalId}/approve")]
    public async Task<IActionResult> ApproveFinalResponse(string approvalId, [FromBody] ApprovalDecisionRequest request, CancellationToken ct)
    {
        var approval = await _finalResponseApproval.ResolveApprovalAsync(approvalId, FinalResponseApprovalStatus.Approved, request.DecidedBy, request.Comment, ct);
        if (approval is null)
            return NotFound(new { error = $"Final approval '{approvalId}' not found." });

        return Ok(approval);
    }

    [HttpPost("final-approvals/{approvalId}/reject")]
    public async Task<IActionResult> RejectFinalResponse(string approvalId, [FromBody] ApprovalDecisionRequest request, CancellationToken ct)
    {
        var approval = await _finalResponseApproval.ResolveApprovalAsync(approvalId, FinalResponseApprovalStatus.Rejected, request.DecidedBy, request.Comment, ct);
        if (approval is null)
            return NotFound(new { error = $"Final approval '{approvalId}' not found." });

        return Ok(approval);
    }

    [HttpGet("runtime/metrics")]
    public async Task<IActionResult> GetRuntimeMetrics([FromQuery] string? sessionId, CancellationToken ct)
    {
        var metrics = await _runtimeCoordinator.GetMetricsAsync(sessionId, ct);
        return Ok(metrics);
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

    // ── Operational Store Query Endpoints ──────────────────────────────

    /// <summary>
    /// Consulta artefatos com filtros (requer PostgreSQL)
    /// </summary>
    [HttpGet("runtime/artifacts/query")]
    public async Task<IActionResult> QueryArtifacts(
        [FromQuery] string? sessionId,
        [FromQuery] string? type,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int limit = 100,
        CancellationToken ct = default)
    {
        if (_operationalStore is null) return StatusCode(503, new { error = "Operational store não configurado." });

        AgentExecutionArtifactType? artifactType = null;
        if (!string.IsNullOrEmpty(type) && Enum.TryParse<AgentExecutionArtifactType>(type, true, out var parsed))
            artifactType = parsed;

        var artifacts = await _operationalStore.QueryArtifactsAsync(sessionId, artifactType, from, to, limit, ct);
        return Ok(artifacts);
    }

    /// <summary>
    /// Histórico de snapshots de métricas (requer PostgreSQL)
    /// </summary>
    [HttpGet("runtime/metrics/history")]
    public async Task<IActionResult> GetMetricsHistory(
        [FromQuery] string? sessionId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int limit = 50,
        CancellationToken ct = default)
    {
        if (_operationalStore is null) return StatusCode(503, new { error = "Operational store não configurado." });

        var history = await _operationalStore.GetMetricsHistoryAsync(sessionId, from, to, limit, ct);
        return Ok(history);
    }

    /// <summary>
    /// Consulta avaliações de runtime (requer PostgreSQL)
    /// </summary>
    [HttpGet("runtime/evaluations")]
    public async Task<IActionResult> GetEvaluations(
        [FromQuery] string? agentName,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken ct = default)
    {
        if (_operationalStore is null) return StatusCode(503, new { error = "Operational store não configurado." });

        var evaluations = await _operationalStore.GetEvaluationsAsync(sessionId: null, agentName, from, to, ct: ct);
        return Ok(evaluations);
    }

    /// <summary>
    /// Executa avaliação de qualidade em tempo real (requer PostgreSQL)
    /// </summary>
    [HttpGet("runtime/evaluate")]
    public async Task<IActionResult> EvaluateRuntime(
        [FromQuery] string? sessionId,
        [FromQuery] string? agentName,
        CancellationToken ct = default)
    {
        if (_runtimeEvaluator is null) return StatusCode(503, new { error = "Runtime evaluator não configurado." });

        var result = await _runtimeEvaluator.EvaluateAsync(sessionId, agentName, ct);
        return Ok(result);
    }

    /// <summary>
    /// Detecta regressões de qualidade recentes (requer PostgreSQL)
    /// </summary>
    [HttpGet("runtime/regressions")]
    public async Task<IActionResult> DetectRegressions(
        [FromQuery] DateTime? since,
        CancellationToken ct = default)
    {
        if (_runtimeEvaluator is null) return StatusCode(503, new { error = "Runtime evaluator não configurado." });

        var regressions = await _runtimeEvaluator.DetectRegressionsAsync(since, ct);
        return Ok(regressions);
    }

    /// <summary>
    /// Modelo de requisição contendo a string de YAML.
    /// </summary>
    public record YamlRequest(string Yaml);

    /// <summary>
    /// Valida o YAML de configuração declarativa de um agente.
    /// </summary>
    [HttpPost("agents/validate-yaml")]
    public async Task<IActionResult> ValidateYaml([FromBody] YamlRequest request, CancellationToken ct)
    {
        var validator = new AgentYamlValidator(_toolManager);
        var result = await validator.ValidateAsync(request.Yaml, ct);
        return Ok(result);
    }

    /// <summary>
    /// Salva ou atualiza um agente a partir de sua especificação em YAML declarativo.
    /// </summary>
    [HttpPost("agents/save-yaml")]
    public async Task<IActionResult> SaveAgentYaml([FromBody] YamlRequest request, CancellationToken ct)
    {
        var validator = new AgentYamlValidator(_toolManager);
        var result = await validator.ValidateAsync(request.Yaml, ct);
        if (!result.IsValid || result.Specification is null)
        {
            return BadRequest(result);
        }

        var spec = result.Specification;
        
        // Remove agente existente se houver
        var agents = await _agentFactory.GetAllAgentsAsync();
        var existing = agents.FirstOrDefault(a => a.Name.Equals(spec.Name, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            await _agentFactory.RemoveAgentAsync(spec.Name);
        }

        var agent = await _agentFactory.CreateCustomAgentAsync(spec);

        AgentVersion? version = null;
        if (_versioningService is not null)
        {
            try
            {
                version = await _versioningService.CreateVersionAsync(
                    spec.Name,
                    description: "Salvo via YAML Declarativo",
                    changeLog: "Declarative YAML Update",
                    createdBy: "UI-Editor",
                    ct: ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Erro ao registrar a versão do agente {Name} no versioning store.", spec.Name);
            }
        }

        return Ok(new
        {
            Agent = new AgentInfo
            {
                Name = agent.Name,
                Description = agent.Description,
                Tier = agent.Tier,
                IsActive = agent.IsActive,
                CreatedAt = agent.CreatedAt
            },
            Version = version
        });
    }

    /// <summary>
    /// Obtém o histórico de versões registradas de um agente.
    /// </summary>
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

    /// <summary>
    /// Restaura um agente para uma versão de configuração específica (Rollback).
    /// </summary>
    [HttpPost("agents/{name}/rollback/{versionId}")]
    public async Task<IActionResult> RollbackAgent(string name, string versionId, [FromQuery] string rolledBackBy = "UI-Editor", CancellationToken ct = default)
    {
        if (_versioningService is null)
        {
            return StatusCode(503, new { error = "Serviço de versionamento não disponível." });
        }

        var rollbackResult = await _versioningService.RollbackAsync(name, versionId, rolledBackBy, ct);
        if (!rollbackResult.Success || rollbackResult.Version is null)
        {
            return BadRequest(rollbackResult);
        }

        var targetVersion = rollbackResult.Version;
        var spec = new AgentSpecification
        {
            Name = name,
            Instructions = targetVersion.SystemPrompt ?? string.Empty,
            AllowedTools = targetVersion.Tools?.ToList() ?? new List<string>(),
            Description = targetVersion.Description ?? "Versão restaurada via rollback"
        };

        if (targetVersion.Parameters is not null)
        {
            if (targetVersion.Parameters.TryGetValue("model", out var modelObj) && modelObj is not null)
            {
                spec.Configuration["model"] = modelObj.ToString()!;
            }
            if (targetVersion.Parameters.TryGetValue("temperature", out var tempObj) && tempObj is not null && double.TryParse(tempObj.ToString(), out var tempVal))
            {
                spec.Configuration["temperature"] = tempVal;
            }
        }

        var agents = await _agentFactory.GetAllAgentsAsync();
        var existing = agents.FirstOrDefault(a => a.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            spec.Tier = existing.Tier;
            spec.Domain = existing.Domain;
            spec.AutonomyLevel = existing.AutonomyLevel;
        }

        await _agentFactory.RemoveAgentAsync(name);
        var restoredAgent = await _agentFactory.CreateCustomAgentAsync(spec);

        return Ok(new
        {
            Message = rollbackResult.Message,
            Agent = new AgentInfo
            {
                Name = restoredAgent.Name,
                Description = restoredAgent.Description,
                Tier = restoredAgent.Tier,
                IsActive = restoredAgent.IsActive,
                CreatedAt = restoredAgent.CreatedAt
            },
            Version = targetVersion
        });
    }
}

public record ApprovalDecisionRequest(string DecidedBy, string? Comment = null);
