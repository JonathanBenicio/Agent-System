using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol.Transport;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Infrastructure.MCP;

/// <summary>
/// Plugin MCP real — conecta a um MCP Server via stdio ou SSE usando o SDK oficial.
/// </summary>
public class McpClientPlugin : IMCPPlugin, IAsyncDisposable
{
    private readonly MCPPluginConfig _config;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<McpClientPlugin> _logger;
    private IMcpClient? _client;
    private List<string> _tools = new();
    private List<string> _resources = new();
    private MCPPluginStatus _status = MCPPluginStatus.Stopped;

    public string Id => _config.Id;
    public string Name => _config.Name;
    public string Description => _config.Description;
    public string Version => _client?.ServerInfo?.Version ?? "unknown";
    public bool IsEnabled => _status == MCPPluginStatus.Running;
    public IReadOnlyList<string> ProvidedTools => _tools.AsReadOnly();
    public IReadOnlyList<string> ProvidedResources => _resources.AsReadOnly();
    public MCPPluginStatus Status => _status;
    public MCPPluginConfig Config => _config;

    public McpClientPlugin(MCPPluginConfig config, ILoggerFactory loggerFactory)
    {
        _config = config;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<McpClientPlugin>();
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        _status = MCPPluginStatus.Starting;
        _logger.LogInformation("Initializing MCP plugin '{Name}' via {Transport}...", _config.Name, _config.TransportType);

        try
        {
            var transport = CreateTransport();
            _client = await McpClientFactory.CreateAsync(
                transport,
                new McpClientOptions
                {
                    ClientInfo = new() { Name = "AgenticSystem", Version = "1.0.0" }
                },
                _loggerFactory,
                ct);

            // Discover tools
            var tools = await _client.ListToolsAsync(cancellationToken: ct);
            _tools = tools.Select(t => t.Name).ToList();

            // Discover resources (optional capability)
            try
            {
                var resources = await _client.ListResourcesAsync(ct);
                _resources = resources.Select(r => r.Uri).ToList();
            }
            catch
            {
                _resources = new();
            }

            _status = MCPPluginStatus.Running;
            _logger.LogInformation(
                "MCP plugin '{Name}' initialized: {ToolCount} tools, {ResourceCount} resources",
                _config.Name, _tools.Count, _resources.Count);
        }
        catch (Exception ex)
        {
            _status = MCPPluginStatus.Error;
            _logger.LogError(ex, "Failed to initialize MCP plugin '{Name}'", _config.Name);
            throw;
        }
    }

    public async Task<MCPResponse> ExecuteToolAsync(string toolName, Dictionary<string, object> parameters, CancellationToken ct = default)
    {
        if (_client is null || _status != MCPPluginStatus.Running)
            return MCPResponse.Fail($"Plugin '{Name}' is not running.");

        if (!_tools.Contains(toolName))
            return MCPResponse.Fail($"Tool '{toolName}' not found in plugin '{Name}'.");

        try
        {
            _logger.LogDebug("Executing tool '{Tool}' on plugin '{Plugin}'", toolName, Name);

            IReadOnlyDictionary<string, object?> arguments = parameters
                .ToDictionary(static pair => pair.Key, static pair => (object?)pair.Value);

            var result = await _client.CallToolAsync(
                toolName,
                arguments,
                cancellationToken: ct);

            var content = result.Content
                .Where(c => c.Type == "text")
                .Select(c => c.Text)
                .ToList();

            var responseData = content.Count == 1 ? (object)content[0]! : content;

            return new MCPResponse
            {
                Success = !result.IsError,
                Data = responseData,
                ErrorMessage = result.IsError ? string.Join("\n", content) : null,
                Metadata = new Dictionary<string, object>
                {
                    ["pluginId"] = Id,
                    ["toolName"] = toolName
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing tool '{Tool}' on plugin '{Plugin}'", toolName, Name);
            return MCPResponse.Fail($"Execution error: {ex.Message}");
        }
    }

    public async Task<MCPResource> GetResourceAsync(string resourceUri, CancellationToken ct = default)
    {
        if (_client is null || _status != MCPPluginStatus.Running)
            return new MCPResource { Uri = resourceUri, Content = "Plugin not running" };

        try
        {
            var result = await _client.ReadResourceAsync(resourceUri, ct);
            var content = result.Contents.FirstOrDefault();
            var text = content is ModelContextProtocol.Protocol.Types.TextResourceContents textContent
                ? textContent.Text
                : string.Empty;

            return new MCPResource
            {
                Uri = resourceUri,
                MimeType = content?.MimeType ?? "text/plain",
                Content = text
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading resource '{Uri}' from plugin '{Plugin}'", resourceUri, Name);
            return new MCPResource { Uri = resourceUri, Content = $"Error: {ex.Message}" };
        }
    }

    public async Task ShutdownAsync(CancellationToken ct = default)
    {
        if (_client is not null)
        {
            _logger.LogInformation("Shutting down MCP plugin '{Name}'...", Name);
            await _client.DisposeAsync();
            _client = null;
        }
        _status = MCPPluginStatus.Stopped;
        _tools.Clear();
        _resources.Clear();
    }

    public async ValueTask DisposeAsync()
    {
        await ShutdownAsync();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Retorna tools com schema completo (para discovery dinâmico).
    /// </summary>
    public async Task<IReadOnlyList<MCPToolDetail>> GetToolDetailsAsync(CancellationToken ct = default)
    {
        if (_client is null) return Array.Empty<MCPToolDetail>();

        var tools = await _client.ListToolsAsync(cancellationToken: ct);
        return tools.Select(t => new MCPToolDetail
        {
            PluginId = Id,
            PluginName = Name,
            ToolName = t.Name,
            Description = t.Description ?? string.Empty,
            JsonSchema = t.JsonSchema.ValueKind != System.Text.Json.JsonValueKind.Undefined ? t.JsonSchema.ToString() : null
        }).ToList();
    }

    private IClientTransport CreateTransport()
    {
        return _config.TransportType switch
        {
            MCPTransportType.Stdio => new StdioClientTransport(new StdioClientTransportOptions
            {
                Command = _config.Command ?? throw new InvalidOperationException("Command is required for stdio transport."),
                Arguments = _config.Arguments,
                WorkingDirectory = _config.WorkingDirectory,
                EnvironmentVariables = _config.EnvironmentVariables,
                Name = _config.Name
            }, _loggerFactory),

            MCPTransportType.Sse => new SseClientTransport(new SseClientTransportOptions
            {
                Endpoint = new Uri(_config.Endpoint ?? throw new InvalidOperationException("Endpoint is required for SSE transport.")),
                AdditionalHeaders = _config.Headers,
                Name = _config.Name
            }, _loggerFactory),

            _ => throw new NotSupportedException($"Transport type '{_config.TransportType}' not supported.")
        };
    }
}
