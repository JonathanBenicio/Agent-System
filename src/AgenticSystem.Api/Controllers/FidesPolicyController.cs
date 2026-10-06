using System.Security.Claims;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgenticSystem.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/security/fides/policy")]
public sealed class FidesPolicyController : ControllerBase
{
    private readonly IFidesTenantPolicyStore _policyStore;
    private readonly IAuditLog _auditLog;
    private readonly ITenantContextAccessor _tenantContextAccessor;

    public FidesPolicyController(
        IFidesTenantPolicyStore policyStore,
        IAuditLog auditLog,
        ITenantContextAccessor tenantContextAccessor)
    {
        _policyStore = policyStore;
        _auditLog = auditLog;
        _tenantContextAccessor = tenantContextAccessor;
    }

    [HttpGet]
    public async Task<ActionResult<FidesTenantPolicy>> Get(CancellationToken ct)
    {
        if (!IsTenantAdmin())
            return Forbid();

        return Ok(await _policyStore.GetAsync(ct));
    }

    [HttpPut]
    public async Task<ActionResult<FidesTenantPolicy>> Update(
        [FromBody] UpdateFidesPolicyRequest request,
        CancellationToken ct)
    {
        if (!IsTenantAdmin())
            return Forbid();

        var actorId = GetActorId();
        FidesTenantPolicy policy;
        try
        {
            policy = await _policyStore.SaveAsync(request.EnabledDetectors, actorId, ct);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }

        await _auditLog.RecordAsync(new AuditEntry
        {
            Category = AuditCategory.PermissionChange,
            Action = "Fides.PolicyUpdated",
            UserId = actorId,
            TenantId = _tenantContextAccessor.CurrentTenantId,
            Description = "Tenant FIDES detector policy updated.",
            Metadata = new Dictionary<string, object>
            {
                ["policyVersion"] = policy.Version,
                ["enabledDetectors"] = policy.EnabledDetectors
            }
        }, ct);

        return Ok(policy);
    }

    private bool IsTenantAdmin() => User.IsInRole("Owner") || User.IsInRole("Admin");

    private string GetActorId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue("sub")
        ?? throw new InvalidOperationException("Authenticated user has no subject identifier.");
}

public sealed class UpdateFidesPolicyRequest
{
    public Dictionary<string, bool> EnabledDetectors { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}
