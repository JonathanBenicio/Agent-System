namespace AgenticSystem.Core.Interfaces;

public interface IToolManager
{
    Task<ToolResult> ExecuteToolAsync(string toolId, ToolInput input, CancellationToken ct = default);
    Task<IEnumerable<ITool>> GetAvailableToolsAsync(string? category = null);
    void RegisterTool(ITool tool);
    bool UnregisterTool(string toolId);
    ITool? GetTool(string toolId);
}
