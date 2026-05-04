using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Infrastructure.MCP;

/// <summary>
/// Gerenciador de plugins MCP — carrega, descarrega e executa tools via SDK oficial.
/// Suporta transporte stdio (processos locais) e SSE (servidores remotos).
/// </summary>
public class MCPPluginManager : IMCPPluginManager
{
    private readonly ConcurrentDictionary<string, McpClientPlugin> _plugins = new();
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<MCPPluginManager> _logger;

    public MCPPluginManager(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<MCPPluginManager>();
    }

    /// <summary>
    /// Carrega plugin a partir de config JSON (path para arquivo de config ou JSON inline).
    /// </summary>
    public async Task<IMCPPlugin> LoadPluginAsync(string pluginPath, CancellationToken ct = default)
    {
        _logger.LogInformation("Loading MCP plugin from: {Path}", pluginPath);

        var config = ParsePluginConfig(pluginPath);
        return await LoadPluginFromConfigAsync(config, ct);
    }

    /// <summary>
    /// Carrega plugin a partir de configuração tipada.
    /// </summary>
    public async Task<IMCPPlugin> LoadPluginFromConfigAsync(MCPPluginConfig config, CancellationToken ct = default)
    {
        var plugin = new McpClientPlugin(config, _loggerFactory);
        await plugin.InitializeAsync(ct);

        _plugins[plugin.Id] = plugin;
        _logger.LogInformation(
            "MCP Plugin loaded: {Name} ({ToolCount} tools, {ResourceCount} resources)",
            plugin.Name, plugin.ProvidedTools.Count, plugin.ProvidedResources.Count);

        return plugin;
    }

    public async Task UnloadPluginAsync(string pluginId, CancellationToken ct = default)
    {
        if (_plugins.TryRemove(pluginId, out var plugin))
        {
            await plugin.ShutdownAsync(ct);
            _logger.LogInformation("MCP Plugin unloaded: {Name}", plugin.Name);
        }
    }

    public IEnumerable<IMCPPlugin> GetLoadedPlugins() => _plugins.Values;

    public IMCPPlugin? GetPlugin(string pluginId)
        => _plugins.TryGetValue(pluginId, out var plugin) ? plugin : null;

    public async Task<MCPResponse> ExecutePluginToolAsync(string pluginId, string toolName, Dictionary<string, object> parameters, CancellationToken ct = default)
    {
        if (!_plugins.TryGetValue(pluginId, out var plugin))
            return MCPResponse.Fail($"Plugin '{pluginId}' not found");

        if (!plugin.IsEnabled)
            return MCPResponse.Fail($"Plugin '{pluginId}' is not running");

        return await plugin.ExecuteToolAsync(toolName, parameters, ct);
    }

    public async Task<IEnumerable<MCPToolInfo>> GetAllToolsAsync()
    {
        var tools = new List<MCPToolInfo>();

        foreach (var plugin in _plugins.Values.Where(p => p.IsEnabled))
        {
            var details = await plugin.GetToolDetailsAsync();
            tools.AddRange(details.Select(d => new MCPToolInfo
            {
                PluginId = d.PluginId,
                PluginName = d.PluginName,
                ToolName = d.ToolName,
                Description = d.Description
            }));
        }

        return tools;
    }

    /// <summary>
    /// Retorna detalhes completos de tools (com schema JSON) para discovery dinâmico.
    /// </summary>
    public async Task<IEnumerable<MCPToolDetail>> GetAllToolDetailsAsync()
    {
        var tools = new List<MCPToolDetail>();

        foreach (var plugin in _plugins.Values.Where(p => p.IsEnabled))
        {
            var details = await plugin.GetToolDetailsAsync();
            tools.AddRange(details);
        }

        return tools;
    }

    /// <summary>
    /// Obtém status de saúde de todos os plugins.
    /// </summary>
    public IEnumerable<(string Id, string Name, MCPPluginStatus Status, int ToolCount)> GetPluginStatuses()
    {
        return _plugins.Values.Select(p => (p.Id, p.Name, p.Status, p.ProvidedTools.Count));
    }

    private MCPPluginConfig ParsePluginConfig(string pluginPath)
    {
        // Se é um path para arquivo JSON, ler e deserializar
        if (File.Exists(pluginPath))
        {
            var json = File.ReadAllText(pluginPath);
            return JsonSerializer.Deserialize<MCPPluginConfig>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? throw new InvalidOperationException($"Invalid plugin config at '{pluginPath}'");
        }

        // Se parece JSON inline, deserializar diretamente
        if (pluginPath.TrimStart().StartsWith('{'))
        {
            return JsonSerializer.Deserialize<MCPPluginConfig>(pluginPath, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? throw new InvalidOperationException("Invalid inline plugin config JSON");
        }

        // Se é um comando simples (ex: "npx @modelcontextprotocol/server-github")
        var parts = pluginPath.Split(' ', 2);
        return new MCPPluginConfig
        {
            Name = Path.GetFileNameWithoutExtension(parts[0]),
            TransportType = MCPTransportType.Stdio,
            Command = parts[0],
            Arguments = parts.Length > 1 ? parts[1].Split(' ').ToList() : new()
        };
    }
}
