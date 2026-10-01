using System.Security.Claims;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace AgenticSystem.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/agent/improvements")]
public sealed class AgentSelfImprovementController : ControllerBase
{
    private readonly ISelfImprovementEngine _selfImprovementEngine;
    private readonly bool _enabled;

    public AgentSelfImprovementController(
        ISelfImprovementEngine selfImprovementEngine,
        IOptions<SelfImprovementSettings> options)
    {
        _selfImprovementEngine = selfImprovementEngine;
        _enabled = options.Value.Enabled;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SelfImprovementRecord>>> List(CancellationToken ct)
    {
        if (!_enabled)
            return NotFound();

        return Ok(await _selfImprovementEngine.GetProposalsAsync(ct));
    }

    [HttpPost("{proposalId}/approve")]
    public async Task<IActionResult> Approve(string proposalId, CancellationToken ct)
    {
        if (!_enabled)
            return NotFound();

        if (!IsTenantAdmin())
            return Forbid();

        return await _selfImprovementEngine.ApproveProposalAsync(proposalId, GetActorId(), ct)
            ? NoContent()
            : NotFound();
    }

    [HttpPost("{proposalId}/reject")]
    public async Task<IActionResult> Reject(string proposalId, CancellationToken ct)
    {
        if (!_enabled)
            return NotFound();

        if (!IsTenantAdmin())
            return Forbid();

        return await _selfImprovementEngine.RejectProposalAsync(proposalId, GetActorId(), ct)
            ? NoContent()
            : NotFound();
    }

    [HttpPost("{proposalId}/rollback")]
    public async Task<IActionResult> Rollback(string proposalId, CancellationToken ct)
    {
        if (!_enabled)
            return NotFound();

        if (!IsTenantAdmin())
            return Forbid();

        return await _selfImprovementEngine.RollbackProposalAsync(proposalId, GetActorId(), ct)
            ? NoContent()
            : Conflict(new { error = "Proposal is not applied or cannot be rolled back." });
    }

    private bool IsTenantAdmin() => User.IsInRole("Owner") || User.IsInRole("Admin");

    private string GetActorId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue("sub")
        ?? throw new InvalidOperationException("Authenticated user has no subject identifier.");
}
