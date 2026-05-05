using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using AgenticSystem.Core.Interfaces;

namespace AgenticSystem.Infrastructure.MCP;

/// <summary>
/// Converte MCP plugin tools em AIFunction (M.E.AI) para uso com function calling nativo.
/// Cada tool MCP vira um AIFunction invocável pelo FunctionInvokingChatClient.
/// </summary>
public class McpToolsAIFunctionAdapter
{
    private readonly IMCPPluginManager _pluginManager;
    private readonly ILogger<McpToolsAIFunctionAdapter> _logger;

    public McpToolsAIFunctionAdapter(IMCPPluginManager pluginManager, ILogger<McpToolsAIFunctionAdapter> logger)
    {
        _pluginManager = pluginManager;
        _logger = logger;
    }

    /// <summary>
    /// Retorna todas as MCP tools como AIFunction para injeção em ChatOptions.Tools.
    /// </summary>
    public IReadOnlyList<AITool> GetAvailableTools()
    {
        var tools = new List<AITool>();

        foreach (var plugin in _pluginManager.GetLoadedPlugins())
        {
            if (!plugin.IsEnabled) continue;

            foreach (var toolName in plugin.ProvidedTools)
            {
                var function = CreateAIFunction(plugin.Id, plugin.Name, toolName, plugin.Description);
                tools.Add(function);
            }
        }

        _logger.LogDebug("MCP → AIFunction: {Count} tools disponíveis", tools.Count);
        return tools;
    }

    private AIFunction CreateAIFunction(string pluginId, string pluginName, string toolName, string pluginDescription)
    {
        var qualifiedName = $"{pluginName}_{toolName}";

        return AIFunctionFactory.Create(
            async (AIFunctionArguments args, CancellationToken ct) =>
            {
                var parameters = new Dictionary<string, object>();
                foreach (var kvp in args)
                {
                    if (kvp.Value is not null)
                        parameters[kvp.Key] = kvp.Value;
                }

                _logger.LogDebug("🔧 Function call: {Plugin}/{Tool} with {ParamCount} params",
                    pluginName, toolName, parameters.Count);

                var response = await _pluginManager.ExecutePluginToolAsync(pluginId, toolName, parameters, ct);

                if (!response.Success)
                {
                    _logger.LogWarning("⚠️ MCP tool failed: {Plugin}/{Tool} — {Error}",
                        pluginName, toolName, response.ErrorMessage);
                    return $"Error: {response.ErrorMessage}";
                }

                return response.Data switch
                {
                    string s => s,
                    _ => JsonSerializer.Serialize(response.Data)
                };
            },
            new AIFunctionFactoryOptions
            {
                Name = qualifiedName,
                Description = $"[{pluginName}] {toolName}"
            });
    }
}
