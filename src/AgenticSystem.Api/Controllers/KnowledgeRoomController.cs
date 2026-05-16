using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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

    [HttpGet]
    public async Task<IActionResult> ListRooms(CancellationToken ct = default)
    {
        var tenantId = Request.Headers["X-Tenant-Id"].FirstOrDefault() ?? "default-tenant";
        var rooms = await _roomService.ListRoomsAsync(tenantId, ct);
        return Ok(rooms);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetRoom(string id, CancellationToken ct = default)
    {
        var tenantId = Request.Headers["X-Tenant-Id"].FirstOrDefault() ?? "default-tenant";
        var room = await _roomService.GetRoomAsync(id, tenantId, ct);
        if (room == null) return NotFound();
        return Ok(room);
    }

    [HttpPost]
    public async Task<IActionResult> CreateRoom([FromBody] KnowledgeRoom room, CancellationToken ct = default)
    {
        var tenantId = Request.Headers["X-Tenant-Id"].FirstOrDefault() ?? "default-tenant";
        
        var created = await _roomService.CreateRoomAsync(tenantId, room, ct);
        _logger.LogInformation("Created Knowledge Room: {RoomId} for tenant {TenantId}", created.Id, tenantId);
        
        return CreatedAtAction(nameof(GetRoom), new { id = created.Id }, created);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateRoom(string id, [FromBody] KnowledgeRoom room, CancellationToken ct = default)
    {
        var tenantId = Request.Headers["X-Tenant-Id"].FirstOrDefault() ?? "default-tenant";
        if (id != room.Id) return BadRequest("ID mismatch");
        
        var updated = await _roomService.UpdateRoomAsync(tenantId, room, ct);
        return Ok(updated);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteRoom(string id, CancellationToken ct = default)
    {
        var tenantId = Request.Headers["X-Tenant-Id"].FirstOrDefault() ?? "default-tenant";
        var success = await _roomService.DeleteRoomAsync(id, tenantId, ct);
        if (!success) return NotFound();
        
        return NoContent();
    }
}
