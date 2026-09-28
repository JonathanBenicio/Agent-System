using System.Security.Claims;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.Persistence;
using AgenticSystem.Infrastructure.Persistence.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgenticSystem.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/platform")]
public sealed class PlatformAdminController : ControllerBase
{
    private static readonly TimeSpan MaximumSupportGrantDuration = TimeSpan.FromDays(7);
    private readonly AgenticDbContext _db;
    private readonly ITenantContextAccessor _tenantAccessor;

    public PlatformAdminController(AgenticDbContext db, ITenantContextAccessor tenantAccessor)
    {
        _db = db;
        _tenantAccessor = tenantAccessor;
    }

    [HttpGet("tenants")]
    public async Task<IActionResult> ListTenants(CancellationToken ct)
    {
        if (!await IsPlatformAdministratorAsync(ct)) return Forbid();

        var tenants = await _db.Tenants.AsNoTracking().OrderBy(tenant => tenant.Name).ToListAsync(ct);
        return Ok(tenants.Select(tenant => new TenantSummary(tenant.Id, tenant.Name, tenant.Slug, tenant.Plan, tenant.Limits, tenant.IsActive)));
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
        return await _db.PlatformAdministrators.AsNoTracking().AnyAsync(admin => admin.UserId == userId, ct) ? userId : null;
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
}

public sealed record TenantSummary(string Id, string Name, string Slug, TenantPlan Plan, TenantLimits Limits, bool IsActive);
public sealed record UpdateTenantPlanRequest(string Plan);
public sealed record CreateSupportGrantRequest(string UserId, string Reason, DateTime ExpiresAt);
public sealed record SupportGrantSummary(string Id, string TenantId, string UserId, string Scope, string Reason,
    DateTime GrantedAt, string GrantedBy, DateTime ExpiresAt, DateTime? RevokedAt, string? RevokedBy);
