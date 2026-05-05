namespace AgenticSystem.Core.Interfaces;

using AgenticSystem.Core.Models;

public interface IToolManager
{
    Task<ToolResult> ExecuteToolAsync(string toolId, ToolInput input, CancellationToken ct = default);
    Task<IEnumerable<ITool>> GetAvailableToolsAsync(string? category = null);
    void RegisterTool(ITool tool);
    void RegisterToolVariant(string logicalToolId, ITool tool, string version, string? variantName = null, int rolloutPercentage = 100, bool isDefault = false);
    Task<IReadOnlyList<ToolRegistration>> GetRegistrationsAsync(string logicalToolId, CancellationToken ct = default);
    bool UnregisterTool(string toolId);
    ITool? GetTool(string toolId);
}
