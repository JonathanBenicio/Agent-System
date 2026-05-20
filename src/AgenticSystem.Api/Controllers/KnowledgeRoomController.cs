using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AgenticSystem.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/knowledge/rooms")]
public class KnowledgeRoomController : ControllerBase
{
    private readonly IKnowledgeRoomService _roomService;
    private readonly ILogger<KnowledgeRoomController> _logger;

    public KnowledgeRoomController(IKnowledgeRoomService roomService, ILogger<KnowledgeRoomController> logger)
    {
        _roomService = roomService;
        _logger = logger;
    }

    private string GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.Identity?.Name ?? "system";

    [HttpGet]
    public async Task<IActionResult> ListRooms(CancellationToken ct = default)
    {
        var tenantId = Request.Headers["X-Tenant-Id"].FirstOrDefault() ?? "default-tenant";
        var rooms = await _roomService.ListRoomsAsync(tenantId, GetUserId(), ct);
        return Ok(rooms);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetRoom(string id, CancellationToken ct = default)
    {
        var tenantId = Request.Headers["X-Tenant-Id"].FirstOrDefault() ?? "default-tenant";
        var room = await _roomService.GetRoomAsync(id, tenantId, GetUserId(), ct);
        if (room == null) return NotFound();
        return Ok(room);
    }

    [HttpPost]
    public async Task<IActionResult> CreateRoom([FromBody] KnowledgeRoom room, CancellationToken ct = default)
    {
        var tenantId = Request.Headers["X-Tenant-Id"].FirstOrDefault() ?? "default-tenant";
        
        var created = await _roomService.CreateRoomAsync(tenantId, GetUserId(), room, ct);
        _logger.LogInformation("Created Knowledge Room: {RoomId} for tenant {TenantId}", created.Id, tenantId);
        
        return CreatedAtAction(nameof(GetRoom), new { id = created.Id }, created);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateRoom(string id, [FromBody] KnowledgeRoom room, CancellationToken ct = default)
    {
        var tenantId = Request.Headers["X-Tenant-Id"].FirstOrDefault() ?? "default-tenant";
        if (id != room.Id) return BadRequest("ID mismatch");
        
        try
        {
            var updated = await _roomService.UpdateRoomAsync(tenantId, GetUserId(), room, ct);
            return Ok(updated);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteRoom(string id, CancellationToken ct = default)
    {
        var tenantId = Request.Headers["X-Tenant-Id"].FirstOrDefault() ?? "default-tenant";
        var success = await _roomService.DeleteRoomAsync(id, tenantId, GetUserId(), ct);
        if (!success) return NotFound();
        
        return NoContent();
    }

    [HttpGet("{id}/permissions")]
    public async Task<IActionResult> GetPermissions(string id, CancellationToken ct = default)
    {
        var tenantId = Request.Headers["X-Tenant-Id"].FirstOrDefault() ?? "default-tenant";
        try
        {
            var permissions = await _roomService.GetRoomPermissionsAsync(id, tenantId, GetUserId(), ct);
            return Ok(permissions);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpPost("{id}/permissions")]
    public async Task<IActionResult> UpdatePermission(string id, [FromBody] KnowledgeRoomPermissionRequest request, CancellationToken ct = default)
    {
        var tenantId = Request.Headers["X-Tenant-Id"].FirstOrDefault() ?? "default-tenant";
        try
        {
            var permission = await _roomService.AddOrUpdatePermissionAsync(id, request.UserId, request.Role, tenantId, GetUserId(), ct);
            return Ok(permission);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpDelete("{id}/permissions/{targetUserId}")]
    public async Task<IActionResult> DeletePermission(string id, string targetUserId, CancellationToken ct = default)
    {
        var tenantId = Request.Headers["X-Tenant-Id"].FirstOrDefault() ?? "default-tenant";
        try
        {
            var success = await _roomService.RemovePermissionAsync(id, targetUserId, tenantId, GetUserId(), ct);
            if (!success) return NotFound();
            return NoContent();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }
}

public class KnowledgeRoomPermissionRequest
{
    public string UserId { get; set; } = string.Empty;
    public KnowledgeRoomRole Role { get; set; }
}
