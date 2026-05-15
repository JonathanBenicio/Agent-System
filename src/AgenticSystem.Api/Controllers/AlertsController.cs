using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AgenticSystem.Infrastructure.Persistence;
using AgenticSystem.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgenticSystem.Api.Controllers;

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
    public async Task<IActionResult> GetAlerts([FromQuery] int limit = 50)
    {
        using var context = await _dbContextFactory.CreateDbContextAsync();
        
        var alerts = await context.SystemAlerts
            .OrderByDescending(a => a.CreatedAt)
            .Take(limit)
            .ToListAsync();

        return Ok(alerts);
    }

    [HttpPost("{id}/read")]
    public async Task<IActionResult> MarkAsRead(string id)
    {
        using var context = await _dbContextFactory.CreateDbContextAsync();
        
        var alert = await context.SystemAlerts.FindAsync(id);
        if (alert == null)
        {
            return NotFound();
        }

        alert.IsRead = true;
        await context.SaveChangesAsync();

        return Ok();
    }
}
