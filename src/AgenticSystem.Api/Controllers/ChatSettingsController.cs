using System.Security.Claims;
using AgenticSystem.Api.Services;
using AgenticSystem.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgenticSystem.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/chat/configuration")]
public sealed class ChatSettingsController(
    ChatConfigurationService configuration,
    ITenantContextAccessor tenantAccessor) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();
        var catalog = await configuration.GetCatalogAsync(tenantAccessor.CurrentTenantId, userId, ct);
        return Ok(catalog with { CanManageTenant = User.IsInRole("Owner") || User.IsInRole("Admin") });
    }

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] UpdateChatSettingsRequest request, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();
        try
        {
            return Ok(await configuration.SaveAsync(
                tenantAccessor.CurrentTenantId, userId, request.Provider, request.Model, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
