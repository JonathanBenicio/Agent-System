using System.Security.Claims;
using AgenticSystem.Api.Auth;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.Persistence;
using AgenticSystem.Infrastructure.Persistence.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace AgenticSystem.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/platform")]
public sealed class PlatformAdminController : ControllerBase, IAsyncActionFilter
{
    private static readonly TimeSpan MaximumSupportGrantDuration = TimeSpan.FromDays(7);
    private readonly AgenticDbContext _db;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly ISystemOperationContextAccessor _systemOperations;

    public PlatformAdminController(
        AgenticDbContext db,
        ITenantContextAccessor tenantAccessor,
        ISystemOperationContextAccessor systemOperations)
    {
        _db = db;
        _tenantAccessor = tenantAccessor;
        _systemOperations = systemOperations;
    }

    [NonAction]
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!await PlatformAdminAuthorization.IsPlatformAdministratorAsync(
                _db, _systemOperations, context.HttpContext.User, context.HttpContext.RequestAborted))
        {
            context.Result = Forbid();
            return;
        }

        if (context.RouteData.Values.TryGetValue("tenantId", out var rawTenantId) &&
            rawTenantId is string tenantId && TenantIdPolicy.IsReservedSystemId(tenantId))
        {
            context.Result = NotFound();
            return;
        }

        using var systemScope = _systemOperations.BeginScope(SystemOperationKind.PlatformAdministration);
        _systemOperations.Require(SystemOperationKind.PlatformAdministration);
        await next();
    }

    [HttpGet("tenants")]
    public async Task<IActionResult> ListTenants(CancellationToken ct)
    {
        if (!await IsPlatformAdministratorAsync(ct)) return Forbid();

        var tenants = (await _db.Tenants.AsNoTracking().OrderBy(tenant => tenant.Name).ToListAsync(ct))
            .Where(tenant => !TenantIdPolicy.IsReservedSystemId(tenant.Id));
        return Ok(tenants.Select(tenant => new TenantSummary(tenant.Id, tenant.Name, tenant.Slug, tenant.Plan, tenant.Limits, tenant.IsActive)));
    }

    [HttpGet("alerts")]
    [ProducesResponseType(typeof(IReadOnlyList<SystemAlertResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListPlatformAlerts([FromQuery] int limit = 50, CancellationToken ct = default)
    {
        if (!await IsPlatformAdministratorAsync(ct)) return Forbid();
        using var systemScope = _systemOperations.BeginScope(SystemOperationKind.PlatformAdministration);
        _systemOperations.Require(SystemOperationKind.PlatformAdministration);
        var alerts = await _db.SystemAlerts
            .AsNoTracking()
            .OrderByDescending(alert => alert.CreatedAt)
            .Take(Math.Clamp(limit, 1, 200))
            .ToListAsync(ct);
        return Ok(alerts.Select(SystemAlertResponse.From));
    }

    [HttpPost("alerts/{id}/read")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkPlatformAlertAsRead(string id, CancellationToken ct)
    {
        if (!await IsPlatformAdministratorAsync(ct)) return Forbid();
        using var systemScope = _systemOperations.BeginScope(SystemOperationKind.PlatformAdministration);
        _systemOperations.Require(SystemOperationKind.PlatformAdministration);
        var alert = await _db.SystemAlerts.FirstOrDefaultAsync(item => item.Id == id, ct);
        if (alert is null) return NotFound();

        alert.IsRead = true;
        await _db.SaveChangesAsync(ct);
        return Ok();
    }

    [HttpPut("tenants/{tenantId}/plan")]
    public async Task<IActionResult> UpdateTenantPlan(string tenantId, [FromBody] UpdateTenantPlanRequest request, CancellationToken ct)
    {
        var actorId = await GetPlatformAdministratorIdAsync(ct);
        if (actorId is null) return Forbid();
        if (!Enum.TryParse<TenantPlan>(request.Plan, ignoreCase: true, out var plan))
            return BadRequest(new { error = "Plan must be Free, Pro, or Enterprise." });

        var tenant = await _db.Tenants.FirstOrDefaultAsync(item => item.Id == tenantId, ct);
        if (tenant is null) return NotFound();

        tenant.Plan = plan;
        tenant.UpdatedAt = DateTime.UtcNow;
        using (_tenantAccessor.BeginScope(new TenantContext { TenantId = tenantId, TenantName = tenant.Name }))
        {
            _db.AuditEntries.Add(CreateAudit(tenantId, actorId, "TenantPlanChanged",
                new { plan = plan.ToString(), configuredLimits = tenant.Limits }));
            await _db.SaveChangesAsync(ct);
        }

        return Ok(new TenantSummary(tenant.Id, tenant.Name, tenant.Slug, tenant.Plan, tenant.Limits, tenant.IsActive));
    }

    [HttpGet("tenants/{tenantId}/memberships")]
    public async Task<IActionResult> ListMemberships(string tenantId, CancellationToken ct)
    {
        if (await GetPlatformAdministratorIdAsync(ct) is null) return Forbid();
        if (!await _db.Tenants.IgnoreQueryFilters().AnyAsync(tenant => tenant.Id == tenantId, ct)) return NotFound();

        using var tenantScope = _tenantAccessor.BeginScope(new TenantContext { TenantId = tenantId });
        var memberships = await _db.TenantMemberships.AsNoTracking()
            .Where(item => item.TenantId == tenantId)
            .OrderBy(item => item.SubjectType)
            .ThenBy(item => item.SubjectId)
            .Select(item => new TenantMembershipSummary(item.SubjectId, item.SubjectType, item.Role, item.GrantedAt, item.GrantedBy))
            .ToListAsync(ct);
        return Ok(memberships);
    }

    [HttpPut("tenants/{tenantId}/memberships/{subjectType}/{subjectId}")]
    public async Task<IActionResult> AssignMembership(
        string tenantId,
        string subjectType,
        string subjectId,
        [FromBody] AssignTenantMembershipRequest request,
        CancellationToken ct)
    {
        var actorId = await GetPlatformAdministratorIdAsync(ct);
        if (actorId is null) return Forbid();
        if (string.IsNullOrWhiteSpace(subjectId) || subjectId.Length > 128) return BadRequest(new { error = "SubjectId must contain 1-128 characters." });
        if (!Enum.TryParse<MembershipSubjectType>(subjectType, true, out var parsedSubjectType))
            return BadRequest(new { error = "SubjectType must be User or ApiKey." });

        var role = BuiltInRoles.All.FirstOrDefault(item => item.Name.Equals(request.Role, StringComparison.OrdinalIgnoreCase));
        if (role is null) return BadRequest(new { error = "Role must be Owner, Admin, Operator, or Viewer." });

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        if (!await _db.Tenants.IgnoreQueryFilters().AnyAsync(tenant => tenant.Id == tenantId, ct)) return NotFound();
        using var tenantScope = _tenantAccessor.BeginScope(new TenantContext { TenantId = tenantId });

        if (parsedSubjectType == MembershipSubjectType.ApiKey)
        {
            if (!Guid.TryParse(subjectId, out var apiKeyId)) return BadRequest(new { error = "API key subject ID must be a GUID." });
            var apiKey = await _db.AccessApiKeys.IgnoreQueryFilters()
                .FirstOrDefaultAsync(key => key.Id == apiKeyId && key.TenantId == tenantId, ct);
            if (apiKey is null) return NotFound(new { error = "API key not found in this tenant." });
            apiKey.Role = role.Name;
        }

        var subjectTypeValue = parsedSubjectType.ToString();
        var existingMemberships = await _db.TenantMemberships.IgnoreQueryFilters()
            .Where(item => item.TenantId == tenantId && item.SubjectType == subjectTypeValue && item.SubjectId == subjectId)
            .ToListAsync(ct);
        _db.TenantMemberships.RemoveRange(existingMemberships.Where(item => !item.Role.Equals(role.Name, StringComparison.OrdinalIgnoreCase)));
        var membership = existingMemberships.FirstOrDefault(item => item.Role.Equals(role.Name, StringComparison.OrdinalIgnoreCase));
        if (membership is null)
        {
            membership = new TenantMembershipEntity
            {
                Id = Guid.NewGuid().ToString("N"),
                SubjectId = subjectId,
                SubjectType = subjectTypeValue,
                Role = role.Name,
                TenantId = tenantId,
                GrantedAt = DateTime.UtcNow,
                GrantedBy = actorId
            };
            _db.TenantMemberships.Add(membership);
        }
        else
        {
            membership.GrantedAt = DateTime.UtcNow;
            membership.GrantedBy = actorId;
        }

        if (parsedSubjectType == MembershipSubjectType.User)
        {
            var legacyAssignments = await _db.RoleAssignments.IgnoreQueryFilters()
                .Where(item => item.TenantId == tenantId && item.UserId == subjectId)
                .ToListAsync(ct);
            _db.RoleAssignments.RemoveRange(legacyAssignments.Where(item => !item.RoleId.Equals(role.Name, StringComparison.OrdinalIgnoreCase)));
            var assignment = legacyAssignments.FirstOrDefault(item => item.RoleId.Equals(role.Name, StringComparison.OrdinalIgnoreCase));
            if (assignment is null)
            {
                _db.RoleAssignments.Add(new RoleAssignmentEntity
                {
                    Id = Guid.NewGuid().ToString("N"),
                    UserId = subjectId,
                    RoleId = role.Name,
                    TenantId = tenantId,
                    GrantedBy = actorId,
                    GrantedAt = membership.GrantedAt
                });
            }
            else
            {
                assignment.GrantedBy = actorId;
                assignment.GrantedAt = membership.GrantedAt;
            }
        }

        _db.AuditEntries.Add(CreateAudit(tenantId, actorId, "TenantMembershipAssigned", new
        {
            subjectId,
            subjectType = subjectTypeValue,
            role = role.Name
        }));
        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Ok(ToMembershipSummary(membership));
    }

    [HttpDelete("tenants/{tenantId}/memberships/{subjectType}/{subjectId}")]
    public async Task<IActionResult> RevokeMembership(string tenantId, string subjectType, string subjectId, CancellationToken ct)
    {
        var actorId = await GetPlatformAdministratorIdAsync(ct);
        if (actorId is null) return Forbid();
        if (!Enum.TryParse<MembershipSubjectType>(subjectType, true, out var parsedSubjectType))
            return BadRequest(new { error = "SubjectType must be User or ApiKey." });
        if (subjectId.Length > 128) return BadRequest(new { error = "SubjectId must contain at most 128 characters." });
        if (!await _db.Tenants.IgnoreQueryFilters().AnyAsync(tenant => tenant.Id == tenantId, ct)) return NotFound();

        using var tenantScope = _tenantAccessor.BeginScope(new TenantContext { TenantId = tenantId });
        var subjectTypeValue = parsedSubjectType.ToString();
        var memberships = await _db.TenantMemberships.IgnoreQueryFilters()
            .Where(item => item.TenantId == tenantId && item.SubjectType == subjectTypeValue && item.SubjectId == subjectId)
            .ToListAsync(ct);
        if (memberships.Count == 0) return NoContent();

        _db.TenantMemberships.RemoveRange(memberships);
        if (parsedSubjectType == MembershipSubjectType.User)
        {
            var assignments = await _db.RoleAssignments.IgnoreQueryFilters()
                .Where(item => item.TenantId == tenantId && item.UserId == subjectId)
                .ToListAsync(ct);
            _db.RoleAssignments.RemoveRange(assignments);
        }

        _db.AuditEntries.Add(CreateAudit(tenantId, actorId, "TenantMembershipRevoked", new
        {
            subjectId,
            subjectType = subjectTypeValue,
            roles = memberships.Select(item => item.Role).ToArray()
        }));
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpGet("tenants/{tenantId}/rooms/{roomId}/support-grants")]
    public async Task<IActionResult> ListSupportGrants(string tenantId, string roomId, CancellationToken ct)
    {
        var actorId = await GetPlatformAdministratorIdAsync(ct);
        if (actorId is null) return Forbid();
        using var tenantScope = _tenantAccessor.BeginScope(new TenantContext { TenantId = tenantId });

        var roomExists = await _db.KnowledgeRooms.AnyAsync(room => room.Id == roomId, ct);
        if (!roomExists) return NotFound();
        var grants = await _db.TenantSupportGrants.AsNoTracking()
            .Where(grant => grant.Scope == $"room:{roomId}")
            .OrderByDescending(grant => grant.GrantedAt)
            .ToListAsync(ct);
        return Ok(grants.Select(ToSupportGrantSummary));
    }

    [HttpPost("tenants/{tenantId}/rooms/{roomId}/support-grants")]
    public async Task<IActionResult> CreateSupportGrant(string tenantId, string roomId, [FromBody] CreateSupportGrantRequest request, CancellationToken ct)
    {
        var actorId = await GetPlatformAdministratorIdAsync(ct);
        if (actorId is null) return Forbid();
        if (string.IsNullOrWhiteSpace(request.UserId) || request.UserId.Length > 128 ||
            string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 2000)
            return BadRequest(new { error = "UserId (1-128 characters) and reason (1-2000 characters) are required." });

        var now = DateTime.UtcNow;
        var expiresAt = request.ExpiresAt.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(request.ExpiresAt, DateTimeKind.Utc)
            : request.ExpiresAt.ToUniversalTime();
        if (expiresAt <= now || expiresAt > now + MaximumSupportGrantDuration)
            return BadRequest(new { error = "Support access must expire within the next 7 days." });

        using var tenantScope = _tenantAccessor.BeginScope(new TenantContext { TenantId = tenantId });
        var roomExists = await _db.KnowledgeRooms.AnyAsync(room => room.Id == roomId, ct);
        if (!roomExists) return NotFound(new { error = "Knowledge room not found." });

        var memberExists = await _db.TenantMemberships.AnyAsync(membership =>
            membership.SubjectId == request.UserId && membership.SubjectType == "User", ct);
        if (!memberExists) return BadRequest(new { error = "Support user must already be a member of the tenant." });

        var activeGrantExists = await _db.TenantSupportGrants.AnyAsync(grant =>
            grant.UserId == request.UserId && grant.Scope == $"room:{roomId}" &&
            grant.RevokedAt == null && grant.ExpiresAt > now, ct);
        if (activeGrantExists) return Conflict(new { error = "An active support grant already exists for this user and room." });

        var grant = new TenantSupportGrantEntity
        {
            Id = Guid.NewGuid().ToString("N"),
            TenantId = tenantId,
            UserId = request.UserId,
            Scope = $"room:{roomId}",
            Reason = request.Reason.Trim(),
            GrantedAt = now,
            GrantedBy = actorId,
            ExpiresAt = expiresAt
        };
        _db.TenantSupportGrants.Add(grant);

        var existingPermission = await _db.KnowledgeRoomPermissions.FirstOrDefaultAsync(permission =>
            permission.RoomId == roomId && permission.UserId == request.UserId, ct);
        if (existingPermission is null)
        {
            _db.KnowledgeRoomPermissions.Add(new KnowledgeRoomPermissionEntity
            {
                Id = grant.Id,
                TenantId = tenantId,
                RoomId = roomId,
                UserId = request.UserId,
                Role = KnowledgeRoomRole.Reader.ToString(),
                GrantedAt = now
            });
        }

        _db.AuditEntries.Add(CreateAudit(tenantId, actorId, "TenantSupportGrantCreated", new
        {
            grantId = grant.Id,
            roomId,
            userId = request.UserId,
            reason = grant.Reason,
            expiresAt = grant.ExpiresAt
        }));
        await _db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(ListSupportGrants), new { tenantId, roomId }, ToSupportGrantSummary(grant));
    }

    [HttpDelete("tenants/{tenantId}/rooms/{roomId}/support-grants/{grantId}")]
    public async Task<IActionResult> RevokeSupportGrant(string tenantId, string roomId, string grantId, CancellationToken ct)
    {
        var actorId = await GetPlatformAdministratorIdAsync(ct);
        if (actorId is null) return Forbid();
        using var tenantScope = _tenantAccessor.BeginScope(new TenantContext { TenantId = tenantId });

        var grant = await _db.TenantSupportGrants.FirstOrDefaultAsync(item =>
            item.Id == grantId && item.Scope == $"room:{roomId}", ct);
        if (grant is null) return NotFound();
        if (grant.RevokedAt is not null) return NoContent();

        var now = DateTime.UtcNow;
        grant.RevokedAt = now;
        grant.RevokedBy = actorId;
        var linkedPermission = await _db.KnowledgeRoomPermissions.FirstOrDefaultAsync(permission => permission.Id == grant.Id, ct);
        if (linkedPermission is not null)
            _db.KnowledgeRoomPermissions.Remove(linkedPermission);

        _db.AuditEntries.Add(CreateAudit(tenantId, actorId, "TenantSupportGrantRevoked", new { grantId, roomId, userId = grant.UserId }));
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    private async Task<string?> GetPlatformAdministratorIdAsync(CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (string.IsNullOrWhiteSpace(userId)) return null;
        return await PlatformAdminAuthorization.IsPlatformAdministratorAsync(_db, _systemOperations, User, ct) ? userId : null;
    }

    private async Task<bool> IsPlatformAdministratorAsync(CancellationToken ct) =>
        await GetPlatformAdministratorIdAsync(ct) is not null;

    private static AuditEntryEntity CreateAudit(string tenantId, string actorId, string action, object details) => new()
    {
        Id = Guid.NewGuid().ToString("N"),
        TenantId = tenantId,
        UserId = actorId,
        Timestamp = DateTime.UtcNow,
        Category = AuditCategory.PermissionChange.ToString(),
        Action = action,
        Description = action,
        DetailsJson = System.Text.Json.JsonSerializer.Serialize(details)
    };

    private static SupportGrantSummary ToSupportGrantSummary(TenantSupportGrantEntity grant) => new(
        grant.Id, grant.TenantId, grant.UserId, grant.Scope, grant.Reason,
        grant.GrantedAt, grant.GrantedBy, grant.ExpiresAt, grant.RevokedAt, grant.RevokedBy);

    private static TenantMembershipSummary ToMembershipSummary(TenantMembershipEntity membership) => new(
        membership.SubjectId, membership.SubjectType, membership.Role, membership.GrantedAt, membership.GrantedBy);
}

public sealed record TenantSummary(string Id, string Name, string Slug, TenantPlan Plan, TenantLimits Limits, bool IsActive);
public sealed record UpdateTenantPlanRequest(string Plan);
public sealed record AssignTenantMembershipRequest(string Role);
public sealed record TenantMembershipSummary(string SubjectId, string SubjectType, string Role, DateTime GrantedAt, string? GrantedBy);
public sealed record CreateSupportGrantRequest(string UserId, string Reason, DateTime ExpiresAt);
public sealed record SupportGrantSummary(string Id, string TenantId, string UserId, string Scope, string Reason,
    DateTime GrantedAt, string GrantedBy, DateTime ExpiresAt, DateTime? RevokedAt, string? RevokedBy);

internal enum MembershipSubjectType
{
    User,
    ApiKey
}
