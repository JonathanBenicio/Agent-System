using AgenticSystem.Core.Models;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.Services;
using AgenticSystem.Infrastructure.Configuration;
using AgenticSystem.Infrastructure.Gateway;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace AgenticSystem.Tests;

public class GatewayProviderRegistrationHostedServiceTests
{
    [Fact]
    public async Task StartAsync_RegistersOnlyEnabledPlatformProvidersWithRequiredCredentials()
    {
        var settings = new AgenticSystemSettings
        {
            Ollama = new OllamaSettings { Enabled = false },
            OpenAI = new OpenAISettings { Enabled = true, ApiKey = "platform-openai-key" },
            Gemini = new GeminiSettings { Enabled = true },
            Claude = new ClaudeSettings { Enabled = false, ApiKey = "disabled-claude-key" },
            OpenRouter = new OpenRouterSettings { Enabled = true, ApiKey = "platform-openrouter-key" }
        };
        var gateway = CreateGateway();
        var registry = new GatewayProviderRegistry(gateway, Options.Create(settings));
        var service = new GatewayProviderRegistrationHostedService(registry, Options.Create(settings), new InMemoryPlatformConfigStore());

        await service.StartAsync(CancellationToken.None);

        var registrations = (await gateway.GetAllServicesStatusAsync()).ToArray();
        registrations.Select(registration => registration.Name).Should().BeEquivalentTo("OpenAI", "OpenRouter");
        registrations.Should().OnlyContain(registration => registration.Category == "LLM");
    }

    [Fact]
    public async Task StartAsync_RegistersOllamaWithoutApiKeyWhenEnabled()
    {
        var settings = new AgenticSystemSettings
        {
            Ollama = new OllamaSettings { Enabled = true }
        };
        var gateway = CreateGateway();
        var registry = new GatewayProviderRegistry(gateway, Options.Create(settings));
        var service = new GatewayProviderRegistrationHostedService(registry, Options.Create(settings), new InMemoryPlatformConfigStore());

        await service.StartAsync(CancellationToken.None);

        (await gateway.GetAllServicesStatusAsync()).Should().ContainSingle().Which.Name.Should().Be("Ollama");
    }

    [Fact]
    public async Task Registry_SynchronizesAdministrativeEnablementAndRemoval()
    {
        var gateway = new ServiceGateway(
            new CostTracker(10m),
            Substitute.For<Microsoft.Extensions.Logging.ILogger<ServiceGateway>>());
        var registry = new GatewayProviderRegistry(gateway, Options.Create(new AgenticSystemSettings()));

        registry.Synchronize("OpenAI", enabled: true);
        (await gateway.GetAllServicesStatusAsync()).Select(status => status.Name).Should().ContainSingle("OpenAI");

        registry.Synchronize("OpenAI", enabled: false);
        (await gateway.GetAllServicesStatusAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task StartAsync_RegistersPlatformDatabaseProviderEvenWhenHostConfigDisablesIt()
    {
        var settings = new AgenticSystemSettings
        {
            OpenAI = new OpenAISettings { Enabled = false }
        };
        var platformStore = new InMemoryPlatformConfigStore();
        await platformStore.SetValuesAsync(
        [
            new PlatformConfigValue("llm.providers.openai.enabled", bool.TrueString),
            new PlatformConfigValue("llm.providers.openai.apiKey", "platform-openai-key", IsSecret: true)
        ],
        "platform-admin");
        var gateway = CreateGateway();
        var registry = new GatewayProviderRegistry(gateway, Options.Create(settings));
        var service = new GatewayProviderRegistrationHostedService(registry, Options.Create(settings), platformStore);

        await service.StartAsync(CancellationToken.None);

        (await gateway.GetServiceStatusAsync("OpenAI")).IsEnabled.Should().BeTrue();
    }

    private static ServiceGateway CreateGateway() => new(
        new CostTracker(10m),
        Substitute.For<Microsoft.Extensions.Logging.ILogger<ServiceGateway>>());

}
