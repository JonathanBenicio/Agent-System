using AgenticSystem.Core.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using AgenticSystem.Infrastructure.Configuration;

namespace AgenticSystem.Infrastructure.Gateway;

/// <summary>Registers platform LLM providers configured in host settings when the API starts.</summary>
public sealed class GatewayProviderRegistrationHostedService : IHostedService
{
    private readonly GatewayProviderRegistry _registry;
    private readonly IOptions<AgenticSystemSettings> _settings;
    private readonly IPlatformConfigStore _platformConfigStore;

    public GatewayProviderRegistrationHostedService(
        GatewayProviderRegistry registry,
        IOptions<AgenticSystemSettings> settings,
        IPlatformConfigStore platformConfigStore)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _platformConfigStore = platformConfigStore ?? throw new ArgumentNullException(nameof(platformConfigStore));
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var settings = _settings.Value;
        await RegisterProviderAsync("Ollama", settings.Ollama.Enabled, hostApiKey: null, requiresCredential: false, cancellationToken);
        await RegisterProviderAsync("OpenAI", settings.OpenAI.Enabled, settings.OpenAI.ApiKey, requiresCredential: true, cancellationToken);
        await RegisterProviderAsync("Gemini", settings.Gemini.Enabled, settings.Gemini.ApiKey, requiresCredential: true, cancellationToken);
        await RegisterProviderAsync("Claude", settings.Claude.Enabled, settings.Claude.ApiKey, requiresCredential: true, cancellationToken);
        await RegisterProviderAsync("OpenRouter", settings.OpenRouter.Enabled, settings.OpenRouter.ApiKey, requiresCredential: true, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task RegisterProviderAsync(
        string providerName,
        bool hostEnabled,
        string? hostApiKey,
        bool requiresCredential,
        CancellationToken cancellationToken)
    {
        var prefix = $"llm.providers.{providerName.ToLowerInvariant()}";
        var platformEnabledValue = await _platformConfigStore.GetValueAsync($"{prefix}.enabled", cancellationToken);
        var platformApiKey = await _platformConfigStore.GetValueAsync($"{prefix}.apiKey", cancellationToken);
        var enabled = bool.TryParse(platformEnabledValue, out var configuredEnabled)
            ? configuredEnabled
            : hostEnabled;
        var effectiveApiKey = platformApiKey ?? hostApiKey;

        _registry.Synchronize(
            providerName,
            enabled && (!requiresCredential || !string.IsNullOrWhiteSpace(effectiveApiKey)));
    }
}
