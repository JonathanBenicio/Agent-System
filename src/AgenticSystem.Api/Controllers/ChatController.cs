using AgenticSystem.Api.Extensions;
using AgenticSystem.Api.Helpers;
using AgenticSystem.Api.Models;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AgenticSystem.Api.Controllers;

/// <summary>
/// Handles synchronous and streaming chat requests.
/// Replaces the inline minimal-API endpoints that were in Program.cs.
/// </summary>
[ApiController]
[Route("api/chat")]
[Authorize]
[EnableRateLimiting(RateLimitingServiceCollectionExtensions.TenantChatPolicyName)]
public class ChatController : ControllerBase
{
    private readonly IMetaAgent _metaAgent;

    public ChatController(IMetaAgent metaAgent)
    {
        _metaAgent = metaAgent;
    }

    /// <summary>
    /// Synchronous chat endpoint. Returns a single AgentResponse.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Chat([FromBody] ChatRequest request, [FromServices] TenantContext tenantContext)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            return BadRequest(new { error = "Message is required." });

        if (request.Message.Length > 10_000)
            return BadRequest(new { error = "Message exceeds maximum length of 10000 characters." });

        var userContext = BuildUserContext(request, tenantContext);

        AgentResponse response;
        if (!string.IsNullOrWhiteSpace(request.TargetAgent))
        {
            response = await _metaAgent.ProcessDirectRequestAsync(request.Message, userContext, request.TargetAgent, request.SessionId);
        }
        else
        {
            response = await _metaAgent.ProcessRequestAsync(request.Message, userContext, request.SessionId);
        }

        return Ok(response);
    }

    /// <summary>
    /// Streaming chat endpoint. Returns Server-Sent Events (SSE).
    /// </summary>
    [HttpPost("stream")]
    public async Task<IResult> ChatStream([FromBody] ChatRequest request, [FromServices] TenantContext tenantContext)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            return Results.BadRequest(new { error = "Message is required." });

        if (request.Message.Length > 10_000)
            return Results.BadRequest(new { error = "Message exceeds maximum length of 10000 characters." });

        var userContext = BuildUserContext(request, tenantContext);

        HttpContext.Response.StatusCode = StatusCodes.Status200OK;
        HttpContext.Response.Headers.Append("Cache-Control", "no-cache");
        HttpContext.Response.Headers.Append("X-Accel-Buffering", "no");
        HttpContext.Response.ContentType = "text/event-stream";

        var stream = !string.IsNullOrWhiteSpace(request.TargetAgent)
            ? _metaAgent.ProcessDirectRequestStreamAsync(request.Message, userContext, request.TargetAgent, request.SessionId, HttpContext.RequestAborted)
            : _metaAgent.ProcessRequestStreamAsync(request.Message, userContext, request.SessionId, HttpContext.RequestAborted);

        await foreach (var streamEvent in stream.WithCancellation(HttpContext.RequestAborted))
        {
            await SseWriter.WriteSseEventAsync(HttpContext, streamEvent, HttpContext.RequestAborted);
        }

        return Results.Empty;
    }

    private UserContext BuildUserContext(ChatRequest request, TenantContext tenantContext)
    {
        // Identity from authenticated principal — never trust client-supplied userId
        var authenticatedUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value
            ?? User.Identity?.Name
            ?? "authenticated-user";

        return new UserContext
        {
            UserId = authenticatedUserId,
            Name = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? request.UserName ?? "User",
            TenantId = tenantContext.TenantId ?? Tenant.DefaultTenantId,
            Language = "pt-BR",
            Preferences = ChatRequestPreferencesBuilder.BuildLlmPreferences(request)
        };
    }
}
