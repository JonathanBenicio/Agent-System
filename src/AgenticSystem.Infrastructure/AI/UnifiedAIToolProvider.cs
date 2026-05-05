using AgenticSystem.Core.Interfaces;
using AgenticSystem.Infrastructure.MCP;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Infrastructure.AI;

/// <summary>
/// Unifica ferramentas internas (ITool) e MCP em um único schema AITool.
/// Esse provider é reutilizado por planner e Agent Framework para evitar divergência.
/// </summary>
public class UnifiedAIToolProvider
{
    private readonly IToolManager? _toolManager;
    private readonly McpToolsAIFunctionAdapter? _mcpToolsAdapter;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<UnifiedAIToolProvider> _logger;

    public UnifiedAIToolProvider(
        ILoggerFactory loggerFactory,
        IToolManager? toolManager = null,
        McpToolsAIFunctionAdapter? mcpToolsAdapter = null)
    {
        _loggerFactory = loggerFactory;
        _toolManager = toolManager;
        _mcpToolsAdapter = mcpToolsAdapter;
        _logger = loggerFactory.CreateLogger<UnifiedAIToolProvider>();
    }

    public async Task<IReadOnlyList<AITool>> GetToolsAsync(CancellationToken ct = default)
    {
        var merged = new List<AITool>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (_toolManager is not null)
        {
            var internalTools = await _toolManager.GetAvailableToolsAsync(category: null);
            foreach (var tool in ToolAIFunctionFactory.CreateFromTools(internalTools, _loggerFactory))
            {
                if (names.Add(tool.Name))
                    merged.Add(tool);
            }
        }

        if (_mcpToolsAdapter is not null)
        {
            foreach (var tool in _mcpToolsAdapter.GetAvailableTools())
            {
                if (names.Add(tool.Name))
                    merged.Add(tool);
            }
        }

        _logger.LogDebug("Unified tools loaded: {Count}", merged.Count);
        return merged;
    }
}
