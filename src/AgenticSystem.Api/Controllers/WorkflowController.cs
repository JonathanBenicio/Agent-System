using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Core.Services;
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
    private readonly ITenantContextAccessor _tenantContextAccessor;

    public WorkflowController(IWorkflowStore store, IWorkflowEngine engine, ILogger<WorkflowController> logger, ITenantContextAccessor tenantContextAccessor)
    {
        _store = store;
        _engine = engine;
        _logger = logger;
        _tenantContextAccessor = tenantContextAccessor;
    }

    private string GetTenantId() => _tenantContextAccessor.CurrentTenantId
        ?? throw new UnauthorizedAccessException("Tenant identity is required.");

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
        try { WorkflowGraphValidator.Validate(definition); }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException)
        {
            return BadRequest(new { error = error.Message });
        }
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

        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();
        var execution = await _engine.StartAsync(tenantId, definition, variables, userId, ct);

        // Async HTTP API Pattern: return 202 Accepted — client polls statusUrl for completion.
        // This prevents HTTP timeout on long-running LLM/agent workflows.
        var statusUrl = $"/api/workflow/executions/{Uri.EscapeDataString(execution.Id)}";
        Response.Headers.Location = statusUrl;

        return Accepted(new
        {
            id = execution.Id,
            executionId = execution.Id,
            workflowId = execution.WorkflowId,
            workflowName = execution.WorkflowName,
            status = execution.Status.ToString(),
            startedAt = execution.StartedAt,
            statusUrl,
            message = "Workflow started. Poll statusUrl for completion."
        });
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
        try
        {
            var execution = await _engine.CancelAsync(tenantId, id, reason, ct);
            return Ok(execution);
        }
        catch (System.ArgumentException ex)
        {
            _logger.LogWarning(ex, "Failed to cancel workflow execution: execution {ExecutionId} not found.", id);
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpPost("executions/{id}/approve")]
    public async Task<IActionResult> ApproveExecution(string id, CancellationToken ct = default, [FromQuery] string? stepId = null)
    {
        var tenantId = GetTenantId();
        if (!CanApproveWorkflow()) return Forbid();
        var approver = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(approver)) return Unauthorized();

        try
        {
            return Ok(string.IsNullOrWhiteSpace(stepId)
                ? await _engine.ApproveAsync(tenantId, id, approver, ct)
                : await _engine.ApproveStepAsync(tenantId, id, stepId, approver, ct));
        }
        catch (WorkflowApprovalAmbiguousException ex)
        {
            return Conflict(new { error = ex.Message, pendingStepIds = ex.PendingStepIds });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Approval target not found: {ExecutionId}", id);
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    [HttpPost("executions/{id}/reject")]
    public async Task<IActionResult> RejectExecution(string id, [FromQuery] string? reason, CancellationToken ct = default, [FromQuery] string? stepId = null)
    {
        var tenantId = GetTenantId();
        if (!CanApproveWorkflow()) return Forbid();
        var rejector = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(rejector)) return Unauthorized();

        try
        {
            return Ok(string.IsNullOrWhiteSpace(stepId)
                ? await _engine.RejectAsync(tenantId, id, rejector, reason, ct)
                : await _engine.RejectStepAsync(tenantId, id, stepId, rejector, reason, ct));
        }
        catch (WorkflowApprovalAmbiguousException ex)
        {
            return Conflict(new { error = ex.Message, pendingStepIds = ex.PendingStepIds });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Rejection target not found: {ExecutionId}", id);
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    private bool CanApproveWorkflow() =>
        User.IsInRole("Owner") || User.IsInRole("Admin") || User.IsInRole("Operator");
}
