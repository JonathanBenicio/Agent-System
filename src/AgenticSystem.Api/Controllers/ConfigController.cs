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
    private readonly IConfigReloadNotifier _reloadNotifier;
    private readonly ILogger<ConfigController> _logger;

    public ConfigController(
        IConfigManager configManager,
        IConfigReloadNotifier reloadNotifier,
        ILogger<ConfigController> logger)
    {
        _configManager = configManager;
        _reloadNotifier = reloadNotifier;
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

    /// <summary>
    /// Explicitly triggers a hot-swap for a specific subsystem or all subsystems.
    /// Fires IConfigReloadNotifier so all HotSwappable proxies invalidate their cached instances.
    /// Part of the "Hot-Swapping Foundation" (Phase 0).
    /// </summary>
    [HttpPost("hot-swap")]
    public IActionResult TriggerHotSwap([FromBody] HotSwapRequest request)
    {
        var subsystem = request.Subsystem?.ToLowerInvariant() ?? "all";
        var validSubsystems = new[] { "vectorstore", "llm", "embedding", "all" };

        if (!validSubsystems.Contains(subsystem))
        {
            return BadRequest(new
            {
                error = $"Invalid subsystem '{subsystem}'",
                validSubsystems
            });
        }

        var notificationKey = subsystem switch
        {
            "vectorstore" => "AgenticSystem:Memory",
            "llm" => "AgenticSystem:LLM",
            "embedding" => "AgenticSystem:Embedding",
            "all" => "AgenticSystem:*",
            _ => subsystem
        };

        _reloadNotifier.NotifyChange(notificationKey);
        _logger.LogInformation("🔄 Hot-swap triggered for subsystem: {Subsystem} (key: {Key})", subsystem, notificationKey);

        return Ok(new
        {
            status = "hot_swap_triggered",
            subsystem,
            notificationKey,
            timestamp = DateTime.UtcNow
        });
    }
}

public record HotSwapRequest
{
    /// <summary>
    /// The subsystem to hot-swap: "vectorstore", "llm", "embedding", or "all".
    /// </summary>
    public string? Subsystem { get; init; } = "all";
}
