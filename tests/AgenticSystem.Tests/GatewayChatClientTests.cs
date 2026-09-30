using AgenticSystem.Core.Models;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.LLM.Interfaces;
using AgenticSystem.Core.Services;
using AgenticSystem.Infrastructure.Gateway;
using AgenticSystem.Infrastructure.LLM;
using AgenticSystem.Infrastructure.Configuration;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using LLMRequest = AgenticSystem.Core.LLM.Models.LLMRequest;
using LLMResponse = AgenticSystem.Core.LLM.Models.LLMResponse;

namespace AgenticSystem.Tests;

public class GatewayChatClientTests
{
    [RequiresOllamaFact]
    public async Task GatewayChatClient_SendsRealOllamaRequestAndRecordsHealthyGatewayCall()
    {
        var baseUrl = Environment.GetEnvironmentVariable("AGENTIC_TEST_OLLAMA_URL")!;
        var model = Environment.GetEnvironmentVariable("AGENTIC_TEST_OLLAMA_MODEL") ?? "qwen2.5:0.5b";
        var settings = new OllamaSettings { Enabled = true, BaseUrl = baseUrl, DefaultModel = model };
        using var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
        var provider = new OllamaProvider(httpClient, Options.Create(settings), NullLogger<OllamaProvider>.Instance);
        var gateway = CreateGateway("Ollama");
        var client = new GatewayChatClient(new ProviderBackedChatClient(provider), gateway, "Ollama");

        var response = await client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "Responda em português com uma frase curta: o modelo local está ativo?")],
            new ChatOptions { ModelId = model, MaxOutputTokens = 64 });

        response.Text.Should().NotBeNullOrWhiteSpace();
        response.ModelId.Should().Be(model);
        var status = await gateway.GetServiceStatusAsync("Ollama");
        status.RequestCount.Should().Be(1);
        status.FailureCount.Should().Be(0);
        status.IsHealthy.Should().BeTrue();
    }

    [Fact]
    public async Task ContextAwareChatClient_RoutesConfiguredPlatformProviderThroughGateway()
    {
        var provider = Substitute.For<ILLMProvider>();
        provider.Name.Returns("Ollama");
        provider.DefaultModel.Returns("test-model");
        provider.IsEnabled.Returns(true);
        provider.Priority.Returns(1);
        provider.GenerateAsync(Arg.Any<LLMRequest>(), Arg.Any<CancellationToken>())
            .Returns(LLMResponse.Ok("production route", "test-model", "Ollama"));

        var tenantContext = Substitute.For<ITenantContextAccessor>();
        tenantContext.CurrentTenantId.Returns("tenant-platform-config");
        var platformStore = new InMemoryPlatformConfigStore();
        await platformStore.SetValueAsync(
            "llm.providers.ollama.apiKey",
            "platform-provider-key",
            isSecret: true,
            changedBy: "platform-admin-1");
        using var providerServices = new ServiceCollection()
            .AddSingleton(tenantContext)
            .AddSingleton<IPlatformConfigStore>(platformStore)
            .BuildServiceProvider();
        var manager = new LLMManager([provider], Substitute.For<ILogger<LLMManager>>(), providerServices);
        var gateway = CreateGateway("Ollama");
        var client = new ContextAwareChatClient(
            manager,
            new LLMRuntimeContextAccessor(),
            Substitute.For<IQuotaEnforcer>(),
            Substitute.For<ITokenAuditService>(),
            Substitute.For<ILogger<ContextAwareChatClient>>(),
            gateway);

        var response = await client.GetResponseAsync([new ChatMessage(ChatRole.User, "hello")]);

        response.Text.Should().Be("production route");
        await provider.Received(1).GenerateAsync(Arg.Any<LLMRequest>(), Arg.Any<CancellationToken>());
        provider.Received().Configure("platform-provider-key", Arg.Any<string?>(), Arg.Any<bool?>(), Arg.Any<int?>());
        (await gateway.GetServiceStatusAsync("Ollama")).RequestCount.Should().Be(1);

        var streamingUpdates = new List<string?>();
        await foreach (var update in client.GetStreamingResponseAsync([new ChatMessage(ChatRole.User, "stream hello")]))
            streamingUpdates.Add(update.Text);

        streamingUpdates.Should().Equal("production route");
        (await gateway.GetServiceStatusAsync("Ollama")).RequestCount.Should().Be(2);
    }

    [Fact]
    public async Task ContextAwareChatClient_UsesTenantCredentialWithoutGlobalGatewayStateThenUsesPlatformCredential()
    {
        var tenantId = "tenant-a";
        var tenantContext = Substitute.For<ITenantContextAccessor>();
        tenantContext.CurrentTenantId.Returns(_ => tenantId);
        var tenantConfig = Substitute.For<IConfigManager>();
        tenantConfig.ResolveValueAsync(Arg.Any<string>()).Returns(call =>
        {
            var isApiKey = call.Arg<string>() == "llm.providers.ollama.apiKey";
            return Task.FromResult<string?>(isApiKey && tenantId == "tenant-a" ? "tenant-a-byok" : null);
        });
        var platformStore = new InMemoryPlatformConfigStore();
        await platformStore.SetValueAsync(
            "llm.providers.ollama.apiKey",
            "platform-key",
            isSecret: true,
            changedBy: "platform-admin-1");
        using var providerServices = new ServiceCollection()
            .AddSingleton(tenantContext)
            .AddSingleton(tenantConfig)
            .AddSingleton<IPlatformConfigStore>(platformStore)
            .BuildServiceProvider();

        var provider = Substitute.For<ILLMProvider>();
        provider.Name.Returns("Ollama");
        provider.DefaultModel.Returns("test-model");
        provider.IsEnabled.Returns(true);
        provider.Priority.Returns(1);
        provider.GenerateAsync(Arg.Any<LLMRequest>(), Arg.Any<CancellationToken>())
            .Returns(LLMResponse.Ok("credential route", "test-model", "Ollama"));
        var manager = new LLMManager([provider], Substitute.For<ILogger<LLMManager>>(), providerServices);
        var gateway = CreateGateway("Ollama");
        var client = new ContextAwareChatClient(
            manager,
            new LLMRuntimeContextAccessor(),
            Substitute.For<IQuotaEnforcer>(),
            Substitute.For<ITokenAuditService>(),
            Substitute.For<ILogger<ContextAwareChatClient>>(),
            gateway);

        var tenantResponse = await client.GetResponseAsync([new ChatMessage(ChatRole.User, "tenant request")]);
        tenantResponse.Text.Should().Be("credential route");
        (await gateway.GetServiceStatusAsync("Ollama")).RequestCount.Should().Be(0);

        tenantId = "tenant-b";
        var platformResponse = await client.GetResponseAsync([new ChatMessage(ChatRole.User, "platform request")]);
        platformResponse.Text.Should().Be("credential route");
        (await gateway.GetServiceStatusAsync("Ollama")).RequestCount.Should().Be(1);
    }

    [Fact]
    public async Task GetResponseAsync_ExecutesPlatformProviderThroughGateway()
    {
        var gateway = CreateGateway("OpenAI");
        var provider = Substitute.For<IChatClient>();
        provider.GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(new ChatResponse(new ChatMessage(ChatRole.Assistant, "gateway response")));
        var client = new GatewayChatClient(provider, gateway, "OpenAI");

        var response = await client.GetResponseAsync([new ChatMessage(ChatRole.User, "hello")]);

        response.Text.Should().Be("gateway response");
        (await gateway.GetServiceStatusAsync("OpenAI")).RequestCount.Should().Be(1);
    }

    [Fact]
    public async Task GetStreamingResponseAsync_TracksTheFullProviderStream()
    {
        var gateway = CreateGateway("Ollama");
        var provider = Substitute.For<IChatClient>();
        provider.GetStreamingResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => StreamChunks());
        var client = new GatewayChatClient(provider, gateway, "Ollama");

        var chunks = new List<string>();
        await foreach (var update in client.GetStreamingResponseAsync([new ChatMessage(ChatRole.User, "hello")]))
            chunks.Add(update.Text ?? string.Empty);

        chunks.Should().Equal("one", "two");
        var status = await gateway.GetServiceStatusAsync("Ollama");
        status.RequestCount.Should().Be(1);
        status.FailureCount.Should().Be(0);
    }

    [Fact]
    public async Task GetResponseAsync_ThrowsGatewayRejectionForProviderFallback()
    {
        var gateway = CreateGateway("OpenAI");
        await gateway.DisableServiceAsync("OpenAI");
        var provider = Substitute.For<IChatClient>();
        var client = new GatewayChatClient(provider, gateway, "OpenAI");

        var act = () => client.GetResponseAsync([new ChatMessage(ChatRole.User, "hello")]);

        await act.Should().ThrowAsync<HttpRequestException>().WithMessage("*disabled*");
        await provider.DidNotReceive().GetResponseAsync(
            Arg.Any<IEnumerable<ChatMessage>>(),
            Arg.Any<ChatOptions?>(),
            Arg.Any<CancellationToken>());
    }

    private static ServiceGateway CreateGateway(string name)
    {
        var gateway = new ServiceGateway(new CostTracker(10m), Substitute.For<ILogger<ServiceGateway>>());
        gateway.RegisterService(new ServiceRegistration { Name = name, Category = "LLM" });
        return gateway;
    }

    private static async IAsyncEnumerable<ChatResponseUpdate> StreamChunks()
    {
        yield return new ChatResponseUpdate(ChatRole.Assistant, "one");
        await Task.Yield();
        yield return new ChatResponseUpdate(ChatRole.Assistant, "two");
    }
}

public sealed class RequiresOllamaFactAttribute : FactAttribute
{
    public RequiresOllamaFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AGENTIC_TEST_OLLAMA_URL")))
            Skip = "Set AGENTIC_TEST_OLLAMA_URL to the isolated backend-validation Ollama endpoint.";
    }
}
