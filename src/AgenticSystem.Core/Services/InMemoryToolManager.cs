using Microsoft.Extensions.Logging;
using AgenticSystem.Core.Interfaces;
using System.Collections.Concurrent;

namespace AgenticSystem.Core.Services;

public class InMemoryToolManager : IToolManager
{
    private readonly ConcurrentDictionary<string, ITool> _tools = new();
    private readonly ILogger<InMemoryToolManager> _logger;

    public InMemoryToolManager(ILogger<InMemoryToolManager> logger)
    {
        _logger = logger;
    }

    public async Task<ToolResult> ExecuteToolAsync(string toolId, ToolInput input, CancellationToken ct = default)
    {
        if (!_tools.TryGetValue(toolId, out var tool))
        {
            _logger.LogWarning("🔧 Tool não encontrada: {ToolId}", toolId);
            return ToolResult.Fail($"Tool '{toolId}' não encontrada.");
        }

        if (!await tool.IsAvailableAsync(ct))
        {
            _logger.LogWarning("🔧 Tool indisponível: {ToolId}", toolId);
            return ToolResult.Fail($"Tool '{toolId}' está indisponível.");
        }

        _logger.LogInformation("🔧 Executando tool: {ToolId} | Action: {Action}", toolId, input.Action);

        try
        {
            var result = await tool.ExecuteAsync(input, ct);
            _logger.LogInformation("🔧 Tool {ToolId} executada com sucesso: {Success}", toolId, result.Success);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "🔧 Erro ao executar tool {ToolId}: {Message}", toolId, ex.Message);
            return ToolResult.Fail($"Erro ao executar '{toolId}': {ex.Message}");
        }
    }

    public Task<IEnumerable<ITool>> GetAvailableToolsAsync(string? category = null)
    {
        IEnumerable<ITool> tools = _tools.Values;

        if (!string.IsNullOrWhiteSpace(category) && Enum.TryParse<ToolCategory>(category, true, out var cat))
        {
            tools = tools.Where(t => t.Category == cat);
        }

        return Task.FromResult(tools);
    }

    public void RegisterTool(ITool tool)
    {
        _tools[tool.Id] = tool;
        _logger.LogInformation("🔧 Tool registrada: {ToolName} ({Category})", tool.Name, tool.Category);
    }

    public bool UnregisterTool(string toolId)
    {
        var removed = _tools.TryRemove(toolId, out _);
        if (removed)
            _logger.LogInformation("🔧 Tool removida: {ToolId}", toolId);
        return removed;
    }

    public ITool? GetTool(string toolId)
    {
        _tools.TryGetValue(toolId, out var tool);
        return tool;
    }
}
