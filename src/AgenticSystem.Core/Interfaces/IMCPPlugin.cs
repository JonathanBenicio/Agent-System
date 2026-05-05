using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Interfaces;

/// <summary>
/// Plugin MCP (Model Context Protocol) para extensões externas.
/// </summary>
public interface IMCPPlugin
{
    string Id { get; }
    string Name { get; }
    string Description { get; }
    string Version { get; }
    bool IsEnabled { get; }
    IReadOnlyList<string> ProvidedTools { get; }
    IReadOnlyList<string> ProvidedResources { get; }

    Task InitializeAsync(CancellationToken ct = default);
    Task<MCPResponse> ExecuteToolAsync(string toolName, Dictionary<string, object> parameters, CancellationToken ct = default);
    Task<MCPResource> GetResourceAsync(string resourceUri, CancellationToken ct = default);
    Task ShutdownAsync(CancellationToken ct = default);
}

/// <summary>
/// Gerenciador de plugins MCP.
/// </summary>
public interface IMCPPluginManager
{
    Task<IMCPPlugin> LoadPluginAsync(string pluginPath, CancellationToken ct = default);
    Task UnloadPluginAsync(string pluginId, CancellationToken ct = default);
    IEnumerable<IMCPPlugin> GetLoadedPlugins();
    IMCPPlugin? GetPlugin(string pluginId);
    Task<MCPResponse> ExecutePluginToolAsync(string pluginId, string toolName, Dictionary<string, object> parameters, CancellationToken ct = default);
    Task<IEnumerable<MCPToolInfo>> GetAllToolsAsync();
    Task AutoStartPluginsAsync(IEnumerable<MCPPluginConfig> configs, CancellationToken ct = default);
}

// ═══════════════════════════════════════════════════════════
// MCP Models
// ═══════════════════════════════════════════════════════════

public class MCPResponse
{
    public bool Success { get; set; }
    public object? Data { get; set; }
    public string? ErrorMessage { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();

    public static MCPResponse Ok(object? data = null)
        => new() { Success = true, Data = data };

    public static MCPResponse Fail(string error)
        => new() { Success = false, ErrorMessage = error };
}

public class MCPResource
{
    public string Uri { get; set; } = string.Empty;
    public string MimeType { get; set; } = "text/plain";
    public string Content { get; set; } = string.Empty;
}

public class MCPToolInfo
{
    public string PluginId { get; set; } = string.Empty;
    public string PluginName { get; set; } = string.Empty;
    public string ToolName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
