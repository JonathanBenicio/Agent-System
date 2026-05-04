using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/admin/config")]
public class ConfigController : ControllerBase
{
    private readonly IConfigManager _configManager;
    private readonly ILogger<ConfigController> _logger;

    public ConfigController(IConfigManager configManager, ILogger<ConfigController> logger)
    {
        _configManager = configManager;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] ConfigCategory? category = null)
    {
        var entries = await _configManager.GetAllAsync(category);
        return Ok(entries);
    }

    [HttpGet("{key}")]
    public async Task<IActionResult> Get(string key)
    {
        try
        {
            var entry = await _configManager.GetAsync(key);
            return Ok(entry);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = $"Key '{key}' not found" });
        }
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ConfigEntryRequest request)
    {
        var entry = await _configManager.SetAsync(request);
        return CreatedAtAction(nameof(Get), new { key = entry.Key }, entry);
    }

    [HttpPut("{key}")]
    public async Task<IActionResult> Update(string key, [FromBody] ConfigEntryRequest request)
    {
        try
        {
            var entry = await _configManager.UpdateAsync(key, request);
            return Ok(entry);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = $"Key '{key}' not found" });
        }
    }

    [HttpDelete("{key}")]
    public async Task<IActionResult> Delete(string key)
    {
        try
        {
            await _configManager.DeleteAsync(key);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = $"Key '{key}' not found" });
        }
    }

    [HttpGet("{key}/validate")]
    public async Task<IActionResult> Validate(string key)
    {
        var result = await _configManager.ValidateAsync(key);
        return Ok(result);
    }

    [HttpGet("audit-log")]
    public async Task<IActionResult> GetAuditLog([FromQuery] string? key = null, [FromQuery] int limit = 50)
    {
        limit = Math.Clamp(limit, 1, 500);
        var logs = await _configManager.GetAuditLogAsync(key, limit);
        return Ok(logs);
    }
}
