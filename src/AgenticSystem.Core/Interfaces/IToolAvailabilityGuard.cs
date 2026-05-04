using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Interfaces;

/// <summary>
/// ML20 — Verifica disponibilidade de tools requeridas antes da execução.
/// Integra com IToolManager para validação e IToolDiscoveryService para sugestões.
/// </summary>
public interface IToolAvailabilityGuard
{
    Task<ToolAvailabilityResult> CheckAsync(IReadOnlyList<string> requiredTools, CancellationToken ct = default);
}

/// <summary>
/// Busca MCPs, plugins e ferramentas externas quando tools requeridas não estão disponíveis.
/// </summary>
public interface IToolDiscoveryService
{
    Task<IReadOnlyList<ToolSuggestion>> SearchAsync(IReadOnlyList<string> missingTools, CancellationToken ct = default);
}

// ═══════════════════════════════════════════════════════════
// Models
// ═══════════════════════════════════════════════════════════

public class ToolAvailabilityResult
{
    public bool AllAvailable => MissingTools.Count == 0;
    public bool NoneAvailable => AvailableTools.Count == 0 && MissingTools.Count > 0;
    public double CoverageRatio => RequiredCount > 0
        ? (double)AvailableTools.Count / RequiredCount
        : 1.0;

    public int RequiredCount { get; init; }
    public IReadOnlyList<string> AvailableTools { get; init; } = [];
    public IReadOnlyList<string> MissingTools { get; init; } = [];
    public IReadOnlyList<ToolSuggestion> Suggestions { get; set; } = [];

    public static ToolAvailabilityResult FullCoverage(IReadOnlyList<string> tools) => new()
    {
        RequiredCount = tools.Count,
        AvailableTools = tools,
        MissingTools = []
    };

    public static ToolAvailabilityResult NoCoverage(IReadOnlyList<string> tools) => new()
    {
        RequiredCount = tools.Count,
        AvailableTools = [],
        MissingTools = tools
    };
}

public record ToolSuggestion
{
    public string ToolName { get; init; } = string.Empty;
    public string PackageName { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty; // npm, github, marketplace, internal
    public string Description { get; init; } = string.Empty;
    public string InstallCommand { get; init; } = string.Empty;
    public double RelevanceScore { get; init; }
}
