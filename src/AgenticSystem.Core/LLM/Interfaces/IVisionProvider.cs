using AgenticSystem.Core.LLM.Models;

namespace AgenticSystem.Core.LLM.Interfaces;

/// <summary>
/// Provider de visão computacional (análise de imagens).
/// </summary>
public interface IVisionProvider
{
    string Name { get; }
    string DefaultModel { get; }
    bool IsEnabled { get; }
    int Priority { get; }

    Task<VisionResponse> AnalyzeImageAsync(VisionRequest request, CancellationToken ct = default);
    Task<bool> IsAvailableAsync(CancellationToken ct = default);
}
