using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Api.Hubs;
using AgenticSystem.Api.SignalR;
using System.Security.Claims;

namespace AgenticSystem.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class SessionController : ControllerBase
{
    private readonly ISessionStore _sessionStore;
    private readonly IHubContext<ChatHub> _hubContext;
    private readonly ILogger<SessionController> _logger;
    private readonly IVectorStore _vectorStore;
    private readonly ITenantContextAccessor _tenantContextAccessor;

    public SessionController(ISessionStore sessionStore, IHubContext<ChatHub> hubContext, ILogger<SessionController> logger, IVectorStore vectorStore, ITenantContextAccessor tenantContextAccessor)
    {
        _sessionStore = sessionStore;
        _hubContext = hubContext;
        _logger = logger;
        _vectorStore = vectorStore;
        _tenantContextAccessor = tenantContextAccessor;
    }

    [HttpGet]
    public async Task<IActionResult> GetSessions([FromQuery] int limit = 50, [FromQuery] string? search = null, CancellationToken ct = default)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var sessions = await _sessionStore.GetByTenantAsync(_tenantContextAccessor.CurrentTenantId, userId, limit, ct);

        var items = sessions
            .OrderByDescending(s => s.EndedAt ?? s.StartedAt)
            .Select(SessionDtoMapper.ToListItem)
            .ToList();

        return Ok(items);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetSession(string id, CancellationToken ct = default)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var session = await _sessionStore.GetAsync(id, ct);
        if (session is null || session.UserId != userId || session.TenantId != _tenantContextAccessor.CurrentTenantId)
            return NotFound(new { error = $"Session '{id}' not found." });

        return Ok(SessionDtoMapper.ToDetail(session));
    }

    [HttpGet("{id}/messages")]
    public async Task<IActionResult> GetSessionMessages(string id, CancellationToken ct = default)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var session = await _sessionStore.GetAsync(id, ct);
        if (session is null || session.UserId != userId || session.TenantId != _tenantContextAccessor.CurrentTenantId)
            return NotFound(new { error = $"Session '{id}' not found." });

        return Ok(SessionDtoMapper.ToMessages(session));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteSession(string id, CancellationToken ct = default)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var session = await _sessionStore.GetAsync(id, ct);
        if (session is null || session.UserId != userId || session.TenantId != _tenantContextAccessor.CurrentTenantId)
            return NotFound(new { error = $"Session '{id}' not found." });

        await _sessionStore.DeleteAsync(id, ct);
        _logger.LogInformation("Session {SessionId} deleted by user {UserId}", id, userId);

        try
        {
            await _vectorStore.DeleteCollectionAsync(id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to purge session documents from vector store for session {SessionId}", id);
        }

        await _hubContext.Clients.Group(TenantSignalRGroups.User(_tenantContextAccessor.CurrentTenantId, userId)).SendAsync("SessionDeleted", id, ct);

        return NoContent();
    }

    [HttpPut("{id}/title")]
    public async Task<IActionResult> UpdateSessionTitle(string id, [FromBody] UpdateSessionTitleRequest request, CancellationToken ct = default)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Title))
            return BadRequest(new { error = "Title cannot be empty." });

        var session = await _sessionStore.GetAsync(id, ct);
        if (session is null || session.UserId != userId || session.TenantId != _tenantContextAccessor.CurrentTenantId)
            return NotFound(new { error = $"Session '{id}' not found." });

        session.RuntimeSettings["title"] = request.Title.Trim();
        await _sessionStore.SaveAsync(session, ct);

        _logger.LogInformation("Session {SessionId} title updated by user {UserId}", id, userId);

        await _hubContext.Clients.Group(TenantSignalRGroups.User(_tenantContextAccessor.CurrentTenantId, userId)).SendAsync("SessionUpdated", id, request.Title.Trim(), ct);

        return Ok(new { id = session.Id, title = request.Title.Trim() });
    }

    private string? GetUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");
    }
}
