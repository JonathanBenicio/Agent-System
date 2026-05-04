using Microsoft.Extensions.Logging;
using AgenticSystem.Core.Interfaces;

namespace AgenticSystem.Core.Services;

/// <summary>
/// ML20 — Valida disponibilidade de tools requeridas antes da execução.
/// Se tools estão ausentes, aciona discovery para sugestões.
/// </summary>
public class ToolAvailabilityGuard : IToolAvailabilityGuard
{
    private readonly IToolManager _toolManager;
    private readonly IToolDiscoveryService _discoveryService;
    private readonly ILogger<ToolAvailabilityGuard> _logger;

    public ToolAvailabilityGuard(
        IToolManager toolManager,
        IToolDiscoveryService discoveryService,
        ILogger<ToolAvailabilityGuard> logger)
    {
        _toolManager = toolManager;
        _discoveryService = discoveryService;
        _logger = logger;
    }

    public async Task<ToolAvailabilityResult> CheckAsync(IReadOnlyList<string> requiredTools, CancellationToken ct = default)
    {
        if (requiredTools.Count == 0 || (requiredTools.Count == 1 && requiredTools[0] == "chat"))
        {
            return ToolAvailabilityResult.FullCoverage(requiredTools);
        }

        var available = new List<string>();
        var missing = new List<string>();

        foreach (var toolId in requiredTools)
        {
            var tool = _toolManager.GetTool(toolId);
            if (tool != null && await tool.IsAvailableAsync(ct))
            {
                available.Add(toolId);
            }
            else
            {
                missing.Add(toolId);
            }
        }

        var result = new ToolAvailabilityResult
        {
            RequiredCount = requiredTools.Count,
            AvailableTools = available,
            MissingTools = missing
        };

        if (missing.Count > 0)
        {
            _logger.LogWarning("🔧 Tools ausentes: [{MissingTools}] — cobertura: {Coverage:P0}",
                string.Join(", ", missing), result.CoverageRatio);

            var suggestions = await _discoveryService.SearchAsync(missing, ct);
            result.Suggestions = suggestions;

            if (suggestions.Count > 0)
            {
                _logger.LogInformation("💡 {Count} sugestão(ões) encontrada(s) para tools ausentes", suggestions.Count);
            }
        }
        else
        {
            _logger.LogDebug("✅ Todas as {Count} tools requeridas disponíveis", requiredTools.Count);
        }

        return result;
    }
}
