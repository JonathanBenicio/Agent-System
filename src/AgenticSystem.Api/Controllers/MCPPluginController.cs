using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;
using AgenticSystem.Infrastructure.MCP;
using AgenticSystem.Infrastructure.Persistence;
using AgenticSystem.Infrastructure.Persistence.Entities;

namespace AgenticSystem.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/admin/plugins")]
public class MCPPluginController : ControllerBase
{
    private readonly IMCPPluginManager _pluginManager;
    private readonly IDbContextFactory<AgenticDbContext> _dbContextFactory;
    private readonly ILogger<MCPPluginController> _logger;

    public MCPPluginController(
        IMCPPluginManager pluginManager, 
        IDbContextFactory<AgenticDbContext> dbContextFactory,
        ILogger<MCPPluginController> logger)
    {
        _pluginManager = pluginManager;
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetPlugins()
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        var dbPlugins = await db.McpPlugins.ToListAsync();
        
        var loaded = _pluginManager.GetLoadedPlugins().ToDictionary(p => p.Id);

        var result = dbPlugins.Select(p => {
            loaded.TryGetValue(p.Id, out var plugin);
            return new
            {
                p.Id,
                p.Name,
                Description = p.Description ?? (plugin?.Description),
                Version = plugin?.Version ?? "1.0.0",
                IsEnabled = plugin?.IsEnabled ?? false,
                ToolCount = plugin?.ProvidedTools.Count ?? 0,
                ResourceCount = plugin?.ProvidedResources.Count ?? 0,
                Status = plugin is McpClientPlugin mcp ? mcp.Status.ToString() : (plugin?.IsEnabled == true ? "Running" : "Stopped")
            };
        });

        return Ok(result);
    }

    [HttpGet("{pluginId}")]
    public IActionResult GetPlugin(string pluginId)
    {
        var plugin = _pluginManager.GetPlugin(pluginId);
        if (plugin is null)
            return NotFound(new { error = $"Plugin '{pluginId}' not found." });

        return Ok(new
        {
            plugin.Id,
            plugin.Name,
            plugin.Description,
            plugin.Version,
            plugin.IsEnabled,
            plugin.ProvidedTools,
            plugin.ProvidedResources,
            Status = plugin is McpClientPlugin mcp ? mcp.Status.ToString() : (plugin.IsEnabled ? "Running" : "Stopped")
        });
    }

    [HttpPost("load")]
    public async Task<IActionResult> LoadPlugin([FromBody] MCPPluginConfig config, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(config.Command) && string.IsNullOrWhiteSpace(config.Endpoint))
            return BadRequest(new { error = "Either 'command' (stdio) or 'endpoint' (SSE) is required." });

        try
        {
            if (_pluginManager is MCPPluginManager manager)
            {
                var plugin = await manager.LoadPluginFromConfigAsync(config, ct);
                
                // Persist to DB
                await using var db = await _dbContextFactory.CreateDbContextAsync();
                var entity = await db.McpPlugins.FindAsync(plugin.Id);
                if (entity == null)
                {
                    db.McpPlugins.Add(new McpPluginEntity
                    {
                        Id = plugin.Id,
                        Name = plugin.Name,
                        Description = plugin.Description,
                        ConfigJson = System.Text.Json.JsonSerializer.Serialize(config),
                        AutoStart = true
                    });
                }
                else
                {
                    entity.ConfigJson = System.Text.Json.JsonSerializer.Serialize(config);
                    entity.Name = plugin.Name;
                }
                await db.SaveChangesAsync();

                _logger.LogInformation("Plugin loaded and persisted: {PluginName} ({PluginId})", plugin.Name, plugin.Id);

                return CreatedAtAction(nameof(GetPlugin), new { pluginId = plugin.Id }, new
                {
                    plugin.Id,
                    plugin.Name,
                    plugin.Version,
                    plugin.IsEnabled,
                    plugin.ProvidedTools,
                    plugin.ProvidedResources
                });
            }

            // Fallback para interface base
            var p = await _pluginManager.LoadPluginAsync(config.Command ?? config.Endpoint ?? "", ct);
            return CreatedAtAction(nameof(GetPlugin), new { pluginId = p.Id }, new { p.Id, p.Name });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load plugin '{Name}'", config.Name);
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("{pluginId}")]
    public async Task<IActionResult> UnloadPlugin(string pluginId, CancellationToken ct)
    {
        var plugin = _pluginManager.GetPlugin(pluginId);
        if (plugin is null)
            return NotFound(new { error = $"Plugin '{pluginId}' not found." });

        await _pluginManager.UnloadPluginAsync(pluginId, ct);
        return NoContent();
    }

    [HttpGet("tools")]
    public async Task<IActionResult> GetAllTools()
    {
        if (_pluginManager is MCPPluginManager manager)
        {
            var details = await manager.GetAllToolDetailsAsync();
            return Ok(details);
        }

        var tools = await _pluginManager.GetAllToolsAsync();
        return Ok(tools);
    }

    [HttpPost("{pluginId}/tools/{toolName}/execute")]
    public async Task<IActionResult> ExecutePluginTool(
        string pluginId,
        string toolName,
        [FromBody] Dictionary<string, object> parameters,
        CancellationToken ct)
    {
        var result = await _pluginManager.ExecutePluginToolAsync(pluginId, toolName, parameters, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("{pluginId}/resources")]
    public IActionResult GetPluginResources(string pluginId)
    {
        var plugin = _pluginManager.GetPlugin(pluginId);
        if (plugin is null)
            return NotFound(new { error = $"Plugin '{pluginId}' not found." });

        return Ok(plugin.ProvidedResources);
    }

    [HttpGet("{pluginId}/resources/{*resourceUri}")]
    public async Task<IActionResult> ReadResource(string pluginId, string resourceUri, CancellationToken ct)
    {
        var plugin = _pluginManager.GetPlugin(pluginId);
        if (plugin is null)
            return NotFound(new { error = $"Plugin '{pluginId}' not found." });

        var resource = await plugin.GetResourceAsync(resourceUri, ct);
        return Ok(resource);
    }

    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        if (_pluginManager is MCPPluginManager manager)
        {
            var statuses = manager.GetPluginStatuses()
                .Select(s => new { s.Id, s.Name, Status = s.Status.ToString(), s.ToolCount });
            return Ok(statuses);
        }

        var plugins = _pluginManager.GetLoadedPlugins()
            .Select(p => new { p.Id, p.Name, Status = p.IsEnabled ? "Running" : "Stopped", ToolCount = p.ProvidedTools.Count });
        return Ok(plugins);
    }
}
