using AgenticSystem.Core.LLM.Models;

namespace AgenticSystem.Core.LLM.Interfaces;

public interface ILLMProvider
{
    string Name { get; }
    string DefaultModel { get; }
    bool IsEnabled { get; }
    int Priority { get; }

    Task<LLMResponse> GenerateAsync(LLMRequest request, CancellationToken ct = default);
    Task<bool> IsAvailableAsync(CancellationToken ct = default);
    void Configure(string? apiKey, string? defaultModel, bool? enabled, int? priority);
}
