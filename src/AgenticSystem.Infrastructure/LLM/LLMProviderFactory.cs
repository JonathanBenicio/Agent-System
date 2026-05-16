using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.LLM.Interfaces;
using AgenticSystem.Infrastructure.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AgenticSystem.Infrastructure.LLM;

/// <summary>
/// Factory that resolves the primary/default ILLMProvider implementation based on current configuration.
/// Supporting the "Hot-Swapping Foundation" (Phase 0).
/// </summary>
public sealed class LLMProviderFactory : ILLMProviderFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IOptionsMonitor<AgenticSystemSettings> _optionsMonitor;

    public LLMProviderFactory(
        IServiceProvider serviceProvider,
        IOptionsMonitor<AgenticSystemSettings> optionsMonitor)
    {
        _serviceProvider = serviceProvider;
        _optionsMonitor = optionsMonitor;
    }

    public ILLMProvider Create()
    {
        var settings = _optionsMonitor.CurrentValue;

        // Determines the default provider by priority. This mirrors the previous logic in LLMManager
        // but enables hot-swapping dynamically via the IOptionsMonitor.
        // A full implementation might resolve multiple providers if needed.
        
        if (settings.OpenAI.Enabled && IsHighestPriority(settings, settings.OpenAI.Priority))
        {
            return ActivatorUtilities.CreateInstance<OpenAIProvider>(_serviceProvider);
        }

        if (settings.Claude.Enabled && IsHighestPriority(settings, settings.Claude.Priority))
        {
            return ActivatorUtilities.CreateInstance<ClaudeProvider>(_serviceProvider);
        }

        if (settings.Gemini.Enabled && IsHighestPriority(settings, settings.Gemini.Priority))
        {
            return ActivatorUtilities.CreateInstance<GeminiProvider>(_serviceProvider);
        }

        if (settings.OpenRouter.Enabled && IsHighestPriority(settings, settings.OpenRouter.Priority))
        {
            return ActivatorUtilities.CreateInstance<OpenRouterProvider>(_serviceProvider);
        }

        if (settings.Ollama.Enabled)
        {
            return ActivatorUtilities.CreateInstance<OllamaProvider>(_serviceProvider);
        }

        // Fallback to Ollama or a dummy if none enabled
        return ActivatorUtilities.CreateInstance<OllamaProvider>(_serviceProvider);
    }

    private static bool IsHighestPriority(AgenticSystemSettings settings, int currentPriority)
    {
        int highest = 999;
        if (settings.OpenAI.Enabled) highest = Math.Min(highest, settings.OpenAI.Priority);
        if (settings.Claude.Enabled) highest = Math.Min(highest, settings.Claude.Priority);
        if (settings.Gemini.Enabled) highest = Math.Min(highest, settings.Gemini.Priority);
        if (settings.OpenRouter.Enabled) highest = Math.Min(highest, settings.OpenRouter.Priority);
        if (settings.Ollama.Enabled) highest = Math.Min(highest, settings.Ollama.Priority);

        return currentPriority <= highest;
    }
}
