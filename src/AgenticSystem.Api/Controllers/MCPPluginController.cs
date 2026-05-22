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
    public async Task<IActionResult> GetPlugin(string pluginId)
    {
        var plugin = _pluginManager.GetPlugin(pluginId);
        if (plugin is null)
            return NotFound(new { error = $"Plugin '{pluginId}' not found." });

        var tools = new List<object>();
        if (plugin is McpClientPlugin mcp)
        {
            var details = await mcp.GetToolDetailsAsync();
            tools.AddRange(details.Select(d => new { name = d.ToolName, description = d.Description }));
        }
        else
        {
            tools.AddRange(plugin.ProvidedTools.Select(t => new { name = t, description = "" }));
        }

        // Get config from DB
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        var entity = await db.McpPlugins.FindAsync(pluginId);
        var config = entity != null ? System.Text.Json.JsonSerializer.Deserialize<MCPPluginConfig>(entity.ConfigJson) : null;

        return Ok(new
        {
            plugin.Id,
            plugin.Name,
            plugin.Description,
            plugin.Version,
            plugin.IsEnabled,
            ProvidedTools = plugin.ProvidedTools,
            Tools = tools,
            plugin.ProvidedResources,
            Status = plugin is McpClientPlugin client ? client.Status.ToString() : (plugin.IsEnabled ? "Running" : "Stopped"),
            Config = config
        });
    }

    [HttpPut("{pluginId}")]
    public async Task<IActionResult> UpdatePlugin(string pluginId, [FromBody] MCPPluginConfig? config, CancellationToken ct)
    {
        if (config == null)
            return BadRequest(new { error = "Request body is empty or invalid JSON." });

        await using var db = await _dbContextFactory.CreateDbContextAsync();
        var entity = await db.McpPlugins.FindAsync(pluginId);
        if (entity == null)
            return NotFound(new { error = $"Plugin '{pluginId}' not found in database." });

        // Update entity
        entity.Name = config.Name ?? entity.Name;
        entity.Description = config.Description ?? entity.Description;
        entity.ConfigJson = System.Text.Json.JsonSerializer.Serialize(config);
        
        await db.SaveChangesAsync();

        // Reload plugin if it was already loaded
        if (_pluginManager.GetPlugin(pluginId) != null)
        {
            await _pluginManager.UnloadPluginAsync(pluginId, ct);
            if (_pluginManager is MCPPluginManager manager)
            {
                await manager.LoadPluginFromConfigAsync(config, ct);
            }
        }

        return Ok(new { success = true, message = "Plugin updated and reloaded" });
    }

    [HttpPost("load")]
    public async Task<IActionResult> LoadPlugin([FromBody] MCPPluginConfig? config, CancellationToken ct)
    {
        if (config == null)
            return BadRequest(new { error = "Request body is empty or invalid JSON." });

        if (config.TransportType == MCPTransportType.Stdio && string.IsNullOrWhiteSpace(config.Command))
            return BadRequest(new { error = "Field 'command' is required for stdio transport." });

        if (config.TransportType == MCPTransportType.Sse && string.IsNullOrWhiteSpace(config.Endpoint))
            return BadRequest(new { error = "Field 'endpoint' is required for SSE transport." });

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
            var message = ex.InnerException != null ? $"{ex.Message} ({ex.InnerException.Message})" : ex.Message;
            return BadRequest(new { error = message });
        }
    }

    [HttpDelete("{pluginId}")]
    public async Task<IActionResult> UnloadPlugin(string pluginId, CancellationToken ct)
    {
        var plugin = _pluginManager.GetPlugin(pluginId);
        
        // Unload from memory
        await _pluginManager.UnloadPluginAsync(pluginId, ct);

        // Delete from DB
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        var entity = await db.McpPlugins.FindAsync(pluginId);
        if (entity != null)
        {
            db.McpPlugins.Remove(entity);
            await db.SaveChangesAsync();
        }

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
