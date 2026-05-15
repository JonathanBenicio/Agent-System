using AgenticSystem.Core.Models;

namespace AgenticSystem.Core.Interfaces;

/// <summary>
/// Interface para classificadores ML rápidos (usados em Triggers e Roteamento).
/// </summary>
public interface IMlClassifier
{
    Task<MlClassificationResult> ClassifyAsync(string input, string? modelId = null, CancellationToken ct = default);
}

public class MlClassificationResult
{
    public string Label { get; set; } = string.Empty;
    public float Confidence { get; set; }
    public Dictionary<string, float> Scores { get; set; } = new();
}
