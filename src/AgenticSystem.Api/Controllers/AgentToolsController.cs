using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AgenticSystem.Core.Interfaces;

namespace AgenticSystem.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/agent")]
public class AgentToolsController : ControllerBase
{
    private readonly IToolManager _toolManager;
    private readonly IMCPPluginManager? _pluginManager;

    public AgentToolsController(
        IToolManager toolManager,
        IMCPPluginManager? pluginManager = null)
    {
        _toolManager = toolManager;
        _pluginManager = pluginManager;
    }

    [HttpPost("{toolId}/execute")]
    public async Task<IActionResult> ExecuteTool(string toolId, [FromBody] ToolInput input, CancellationToken ct)
    {
        var result = await _toolManager.ExecuteToolAsync(toolId, input, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetTools([FromQuery] string? category = null)
    {
        var tools = (await _toolManager.GetAvailableToolsAsync(category)).Select(t => new
        {
            t.Id,
            t.Name,
            t.Description,
            category = t.Category.ToString(),
            t.RequiresAuth
        }).ToList();

        if (_pluginManager is not null)
        {
            if (string.IsNullOrEmpty(category) || category.Equals("MCP", StringComparison.OrdinalIgnoreCase))
            {
                var mcpTools = await _pluginManager.GetAllToolsAsync();
                tools.AddRange(mcpTools.Select(mcp => new
                {
                    Id = $"{mcp.PluginName}_{mcp.ToolName}",
                    Name = $"{mcp.PluginName}_{mcp.ToolName}",
                    Description = $"[MCP {mcp.PluginName}] {mcp.Description}",
                    category = "MCP",
                    RequiresAuth = false
                }));
            }
        }

        return Ok(tools);
    }

    [HttpGet("{toolId}")]
    public IActionResult GetTool(string toolId)
    {
        var tool = _toolManager.GetTool(toolId);
        if (tool is null)
            return NotFound(new { error = $"Tool '{toolId}' not found." });

        return Ok(new
        {
            tool.Id,
            tool.Name,
            tool.Description,
            category = tool.Category.ToString(),
            tool.RequiresAuth
        });
    }

    [HttpDelete("{toolId}")]
    public IActionResult DeleteTool(string toolId)
    {
        var removed = _toolManager.UnregisterTool(toolId);
        if (!removed)
            return NotFound(new { error = $"Tool '{toolId}' not found." });

        return NoContent();
    }
}
