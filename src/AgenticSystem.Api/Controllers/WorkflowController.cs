using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgenticSystem.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/workflow")]
public class WorkflowController : ControllerBase
{
    private readonly IWorkflowStore _store;
    private readonly IWorkflowEngine _engine;
    private readonly ILogger<WorkflowController> _logger;

    public WorkflowController(IWorkflowStore store, IWorkflowEngine engine, ILogger<WorkflowController> logger)
    {
        _store = store;
        _engine = engine;
        _logger = logger;
    }

    private string GetTenantId() => Request.Headers["X-Tenant-Id"].FirstOrDefault() ?? "default-tenant";

    // ─── Definitions ───

    [HttpGet("definitions")]
    public async Task<IActionResult> ListDefinitions([FromQuery] int limit = 50, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var definitions = await _store.ListDefinitionsAsync(tenantId, limit, ct);
        return Ok(definitions);
    }

    [HttpGet("definitions/{id}")]
    public async Task<IActionResult> GetDefinition(string id, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var definition = await _store.GetDefinitionAsync(tenantId, id, ct);
        if (definition == null) return NotFound();
        return Ok(definition);
    }

    [HttpPost("definitions")]
    public async Task<IActionResult> SaveDefinition([FromBody] WorkflowDefinition definition, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        await _store.SaveDefinitionAsync(tenantId, definition, ct);
        return Ok(definition);
    }

    [HttpDelete("definitions/{id}")]
    public async Task<IActionResult> DeleteDefinition(string id, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        await _store.DeleteDefinitionAsync(tenantId, id, ct);
        return NoContent();
    }

    // ─── Executions ───

    [HttpPost("executions/start/{definitionId}")]
    public async Task<IActionResult> StartWorkflow(string definitionId, [FromBody] Dictionary<string, object>? variables, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var definition = await _store.GetDefinitionAsync(tenantId, definitionId, ct);
        if (definition == null) return NotFound("Workflow definition not found");

        var userId = User.Identity?.Name ?? "anonymous";
        var execution = await _engine.StartAsync(tenantId, definition, variables, userId, ct);
        
        return Ok(execution);
    }

    [HttpGet("executions/{id}")]
    public async Task<IActionResult> GetExecution(string id, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var execution = await _engine.GetExecutionAsync(tenantId, id, ct);
        if (execution == null) return NotFound();
        return Ok(execution);
    }

    [HttpGet("executions")]
    public async Task<IActionResult> ListExecutions([FromQuery] WorkflowExecutionStatus? status, [FromQuery] int limit = 50, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var executions = await _engine.ListExecutionsAsync(tenantId, status, limit, ct);
        return Ok(executions);
    }

    [HttpPost("executions/{id}/cancel")]
    public async Task<IActionResult> CancelExecution(string id, [FromQuery] string? reason, CancellationToken ct = default)
    {
        var tenantId = GetTenantId();
        var execution = await _engine.CancelAsync(tenantId, id, reason, ct);
        return Ok(execution);
    }
}
