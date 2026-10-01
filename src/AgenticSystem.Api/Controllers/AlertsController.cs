using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AgenticSystem.Infrastructure.Persistence;
using AgenticSystem.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgenticSystem.Api.Controllers;

public sealed record SystemAlertResponse(
    string Id,
    string Type,
    string Severity,
    string Message,
    string? ProviderName,
    double? Percentage,
    DateTime CreatedAt,
    bool IsRead)
{
    public static SystemAlertResponse From(TenantSystemAlertEntity alert) => new(
        alert.Id, alert.Type, alert.Severity, alert.Message, alert.ProviderName,
        alert.Percentage, alert.CreatedAt, alert.IsRead);

    public static SystemAlertResponse From(SystemAlertEntity alert) => new(
        alert.Id, alert.Type, alert.Severity, alert.Message, alert.ProviderName,
        alert.Percentage, alert.CreatedAt, alert.IsRead);
}

[Authorize]
[ApiController]
[Route("api/v1/alerts")]
public class AlertsController : ControllerBase
{
    private readonly IDbContextFactory<AgenticDbContext> _dbContextFactory;

    public AlertsController(IDbContextFactory<AgenticDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<SystemAlertResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAlerts([FromQuery] int limit = 50)
    {
        using var context = await _dbContextFactory.CreateDbContextAsync();
        
        var alerts = await context.TenantSystemAlerts
            .OrderByDescending(a => a.CreatedAt)
            .Take(limit)
            .ToListAsync();

        return Ok(alerts.Select(SystemAlertResponse.From));
    }

    [HttpPost("{id}/read")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkAsRead(string id)
    {
        using var context = await _dbContextFactory.CreateDbContextAsync();
        
        var alert = await context.TenantSystemAlerts.FindAsync(id);
        if (alert == null)
        {
            return NotFound();
        }

        alert.IsRead = true;
        await context.SaveChangesAsync();

        return Ok();
    }
}
