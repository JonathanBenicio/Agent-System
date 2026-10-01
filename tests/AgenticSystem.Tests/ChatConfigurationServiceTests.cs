using AgenticSystem.Api.Services;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.LLM.Interfaces;
using AgenticSystem.Core.LLM.Models;
using AgenticSystem.Core.Models;
using AgenticSystem.Core.Services;
using FluentAssertions;
using NSubstitute;

namespace AgenticSystem.Tests;

public sealed class ChatConfigurationServiceTests
{
    [Fact]
    public async Task SavedPreference_IsAppliedToNewChatButExistingSessionKeepsItsOwnSelection()
    {
        var admin = Substitute.For<ILLMAdministrationService>();
        admin.GetConfigurationAsync(Arg.Any<CancellationToken>()).Returns(new LLMConfigurationInfo
        {
            DefaultProvider = "Ollama", DefaultModel = "small",
            Providers = [new LLMProviderInfo { Name = "Ollama", DefaultModel = "small", IsEnabled = true,
                Models = ["small", "large"] }]
        });
        var preferences = new InMemoryChatSettingsStore();
        var sessions = new InMemorySessionStore();
        var keys = Substitute.For<ILLMProviderApiKeyService>();
        keys.GetKeysByProviderAsync("Ollama", Arg.Any<CancellationToken>())
            .Returns(Array.Empty<LLMProviderApiKey>());
        var sut = new ChatConfigurationService(admin, keys, preferences, sessions);

        await sut.SaveAsync("tenant-a", "user-a", "Ollama", "large");
        var newChat = new UserContext { TenantId = "tenant-a", UserId = "user-a" };
        await sut.ApplyAsync(newChat, null, null, null);
        newChat.Preferences["llm.request.model"].Should().Be("large");
        (await preferences.GetAsync("tenant-a", "user-b")).Should().BeNull();
        (await preferences.GetAsync("tenant-b", "user-a")).Should().BeNull();

        await sessions.SaveAsync(new SessionData
        {
            Id = "existing-chat", TenantId = "tenant-a", UserId = "user-a",
            RuntimeSettings = new() { ["llm.session.provider"] = "Ollama", ["llm.session.model"] = "small" }
        });
        var resumed = new UserContext { TenantId = "tenant-a", UserId = "user-a" };
        await sut.ApplyAsync(resumed, "existing-chat", null, null);
        resumed.Preferences.Should().NotContainKey("llm.request.model");

        await sut.ApplyAsync(resumed, "existing-chat", "Ollama", "large");
        (await sessions.GetAsync("existing-chat"))!.RuntimeSettings["llm.session.model"].Should().Be("large");
        var foreign = () => sut.ApplyAsync(new UserContext { TenantId = "tenant-a", UserId = "user-b" },
            "existing-chat", null, null);
        await foreign.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task DisabledProviderAndUnknownModel_AreRejectedBeforePersistence()
    {
        var admin = Substitute.For<ILLMAdministrationService>();
        admin.GetConfigurationAsync(Arg.Any<CancellationToken>()).Returns(new LLMConfigurationInfo
        {
            DefaultProvider = "Ollama", DefaultModel = "small",
            Providers =
            [
                new LLMProviderInfo { Name = "Ollama", IsEnabled = true, DefaultModel = "small", Models = ["small"] },
                new LLMProviderInfo { Name = "OpenAI", IsEnabled = false, DefaultModel = "gpt", Models = ["gpt"] }
            ]
        });
        var store = new InMemoryChatSettingsStore();
        var keys = Substitute.For<ILLMProviderApiKeyService>();
        keys.GetKeysByProviderAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<LLMProviderApiKey>());
        var sut = new ChatConfigurationService(admin, keys, store, new InMemorySessionStore());
        await ((Func<Task>)(() => sut.SaveAsync("tenant", "user", "OpenAI", "gpt")))
            .Should().ThrowAsync<InvalidOperationException>();
        await ((Func<Task>)(() => sut.SaveAsync("tenant", "user", "Ollama", "unknown")))
            .Should().ThrowAsync<InvalidOperationException>();
        (await store.GetAsync("tenant", "user")).Should().BeNull();
    }
}
