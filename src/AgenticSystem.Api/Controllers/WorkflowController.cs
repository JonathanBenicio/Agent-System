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

    // ─── Definitions ───

    [HttpGet("definitions")]
    public async Task<IActionResult> ListDefinitions([FromQuery] int limit = 50, CancellationToken ct = default)
    {
        var definitions = await _store.ListDefinitionsAsync(limit, ct);
        return Ok(definitions);
    }

    [HttpGet("definitions/{id}")]
    public async Task<IActionResult> GetDefinition(string id, CancellationToken ct = default)
    {
        var definition = await _store.GetDefinitionAsync(id, ct);
        if (definition == null) return NotFound();
        return Ok(definition);
    }

    [HttpPost("definitions")]
    public async Task<IActionResult> SaveDefinition([FromBody] WorkflowDefinition definition, CancellationToken ct = default)
    {
        await _store.SaveDefinitionAsync(definition, ct);
        return Ok(definition);
    }

    [HttpDelete("definitions/{id}")]
    public async Task<IActionResult> DeleteDefinition(string id, CancellationToken ct = default)
    {
        await _store.DeleteDefinitionAsync(id, ct);
        return NoContent();
    }

    // ─── Executions ───

    [HttpPost("executions/start/{definitionId}")]
    public async Task<IActionResult> StartWorkflow(string definitionId, [FromBody] Dictionary<string, object>? variables, CancellationToken ct = default)
    {
        var definition = await _store.GetDefinitionAsync(definitionId, ct);
        if (definition == null) return NotFound("Workflow definition not found");

        var userId = User.Identity?.Name ?? "anonymous";
        var execution = await _engine.StartAsync(definition, variables, userId, ct);
        
        return Ok(execution);
    }

    [HttpGet("executions/{id}")]
    public async Task<IActionResult> GetExecution(string id, CancellationToken ct = default)
    {
        var execution = await _engine.GetExecutionAsync(id, ct);
        if (execution == null) return NotFound();
        return Ok(execution);
    }

    [HttpGet("executions")]
    public async Task<IActionResult> ListExecutions([FromQuery] WorkflowExecutionStatus? status, [FromQuery] int limit = 50, CancellationToken ct = default)
    {
        var executions = await _engine.ListExecutionsAsync(status, limit, ct);
        return Ok(executions);
    }

    [HttpPost("executions/{id}/cancel")]
    public async Task<IActionResult> CancelExecution(string id, [FromQuery] string? reason, CancellationToken ct = default)
    {
        var execution = await _engine.CancelAsync(id, reason, ct);
        return Ok(execution);
    }
}
