namespace AgenticSystem.Core.Models;

/// <summary>
/// Tipo de transporte para conexão com MCP Server.
/// </summary>
public enum MCPTransportType
{
    Stdio,
    Sse
}

/// <summary>
/// Configuração de um plugin MCP (declarativa — armazenada em config ou banco).
/// </summary>
public class MCPPluginConfig
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public MCPTransportType TransportType { get; set; } = MCPTransportType.Stdio;

    // Stdio transport
    public string? Command { get; set; }
    public List<string> Arguments { get; set; } = new();
    public string? WorkingDirectory { get; set; }
    public Dictionary<string, string> EnvironmentVariables { get; set; } = new();

    // SSE transport
    public string? Endpoint { get; set; }
    public Dictionary<string, string> Headers { get; set; } = new();

    public bool AutoStart { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Info detalhada de uma tool exposta por um plugin MCP (inclui schema).
/// </summary>
public class MCPToolDetail
{
    public string PluginId { get; set; } = string.Empty;
    public string PluginName { get; set; } = string.Empty;
    public string ToolName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? JsonSchema { get; set; }
}

/// <summary>
/// Info de um resource exposto por um plugin MCP.
/// </summary>
public class MCPResourceInfo
{
    public string PluginId { get; set; } = string.Empty;
    public string Uri { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? MimeType { get; set; }
}

/// <summary>
/// Info de um prompt exposto por um plugin MCP.
/// </summary>
public class MCPPromptInfo
{
    public string PluginId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

/// <summary>
/// Status de saúde de um plugin MCP.
/// </summary>
public enum MCPPluginStatus
{
    Stopped,
    Starting,
    Running,
    Error,
    Disconnected
}
