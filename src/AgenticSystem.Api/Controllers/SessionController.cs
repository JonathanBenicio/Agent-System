using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using System.Security.Claims;

namespace AgenticSystem.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class SessionController : ControllerBase
{
    private readonly ISessionStore _sessionStore;
    private readonly ILogger<SessionController> _logger;

    public SessionController(ISessionStore sessionStore, ILogger<SessionController> logger)
    {
        _sessionStore = sessionStore;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetSessions([FromQuery] int limit = 50, CancellationToken ct = default)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var sessions = await _sessionStore.GetByUserAsync(userId, limit, ct);

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
        if (session is null || session.UserId != userId)
            return NotFound(new { error = $"Session '{id}' not found." });

        return Ok(SessionDtoMapper.ToDetail(session));
    }

    [HttpGet("{id}/messages")]
    public async Task<IActionResult> GetSessionMessages(string id, CancellationToken ct = default)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var session = await _sessionStore.GetAsync(id, ct);
        if (session is null || session.UserId != userId)
            return NotFound(new { error = $"Session '{id}' not found." });

        return Ok(SessionDtoMapper.ToMessages(session));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteSession(string id, CancellationToken ct = default)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var session = await _sessionStore.GetAsync(id, ct);
        if (session is null || session.UserId != userId)
            return NotFound(new { error = $"Session '{id}' not found." });

        await _sessionStore.DeleteAsync(id, ct);
        _logger.LogInformation("Session {SessionId} deleted by user {UserId}", id, userId);

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
        if (session is null || session.UserId != userId)
            return NotFound(new { error = $"Session '{id}' not found." });

        session.RuntimeSettings["title"] = request.Title.Trim();
        await _sessionStore.SaveAsync(session, ct);

        _logger.LogInformation("Session {SessionId} title updated by user {UserId}", id, userId);

        return Ok(new { id = session.Id, title = request.Title.Trim() });
    }

    private string? GetUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");
    }
}
