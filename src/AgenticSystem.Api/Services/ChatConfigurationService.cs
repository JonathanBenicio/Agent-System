using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.LLM.Interfaces;
using AgenticSystem.Core.Models;

namespace AgenticSystem.Api.Services;

/// <summary>Validates member-visible LLM choices and applies persisted preferences to a chat turn.</summary>
public sealed class ChatConfigurationService(
    ILLMAdministrationService administration,
    ILLMProviderApiKeyService apiKeyService,
    IChatSettingsStore settingsStore,
    ISessionStore sessionStore)
{
    public async Task<ChatCatalog> GetCatalogAsync(string tenantId, string userId, CancellationToken ct = default)
    {
        var configuration = await administration.GetConfigurationAsync(ct);
        var enabled = new List<ChatProviderOption>();
        foreach (var provider in configuration.Providers.Where(item => item.IsEnabled))
        {
            var keys = await apiKeyService.GetKeysByProviderAsync(provider.Name, ct);
            var defaultKeyModels = keys.Where(item => item.IsEnabled && item.IsDefault)
                .SelectMany(item => item.Models).Distinct(StringComparer.OrdinalIgnoreCase);
            var models = provider.Models.Concat(defaultKeyModels).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            enabled.Add(new ChatProviderOption(provider.Name, provider.DefaultModel, models));
        }
        var preferred = await settingsStore.GetAsync(tenantId, userId, ct);
        return new ChatCatalog(configuration.DefaultProvider, configuration.DefaultModel,
            enabled,
            preferred?.Provider, preferred?.Model);
    }

    public async Task<ChatSettings> SaveAsync(
        string tenantId, string userId, string provider, string model, CancellationToken ct = default)
    {
        var catalog = await GetCatalogAsync(tenantId, userId, ct);
        var selected = Validate(catalog, provider, model);
        var settings = new ChatSettings(tenantId, userId, selected.Provider, selected.Model, DateTime.UtcNow);
        await settingsStore.SaveAsync(settings, ct);
        return settings;
    }

    public async Task ApplyAsync(
        UserContext context, string? sessionId, string? requestedProvider, string? requestedModel,
        CancellationToken ct = default)
    {
        var catalog = await GetCatalogAsync(context.TenantId, context.UserId, ct);
        SessionData? session = null;
        if (!string.IsNullOrWhiteSpace(sessionId))
        {
            session = await sessionStore.GetAsync(sessionId, ct);
            if (session is null || session.UserId != context.UserId || session.TenantId != context.TenantId || session.EndedAt is not null)
                throw new InvalidOperationException("Session is unavailable for this user and tenant.");
        }

        var explicitChoice = !string.IsNullOrWhiteSpace(requestedProvider) || !string.IsNullOrWhiteSpace(requestedModel);
        if (session is not null && !explicitChoice &&
            session.RuntimeSettings.TryGetValue("llm.session.provider", out var sessionProvider) &&
            session.RuntimeSettings.TryGetValue("llm.session.model", out var sessionModel))
        {
            Validate(catalog, sessionProvider, sessionModel);
            return;
        }

        var provider = requestedProvider ?? (session is not null && session.RuntimeSettings.TryGetValue("llm.session.provider", out var prior)
            ? prior : catalog.PreferredProvider ?? catalog.DefaultProvider);
        var option = catalog.Providers.FirstOrDefault(item => item.Name.Equals(provider, StringComparison.OrdinalIgnoreCase));
        var model = requestedModel ?? (session is not null && session.RuntimeSettings.TryGetValue("llm.session.model", out var priorModel)
            && string.Equals(provider, session.RuntimeSettings.GetValueOrDefault("llm.session.provider"), StringComparison.OrdinalIgnoreCase)
            ? priorModel : catalog.PreferredProvider == provider ? catalog.PreferredModel : option?.DefaultModel);
        var selected = Validate(catalog, provider, model);

        context.Preferences["llm.request.provider"] = selected.Provider;
        context.Preferences["llm.request.model"] = selected.Model;
        context.Preferences["llm.session.provider"] = selected.Provider;
        context.Preferences["llm.session.model"] = selected.Model;

        if (session is not null && (explicitChoice ||
            !session.RuntimeSettings.ContainsKey("llm.session.provider") ||
            !session.RuntimeSettings.ContainsKey("llm.session.model")))
        {
            session.RuntimeSettings["llm.session.provider"] = selected.Provider;
            session.RuntimeSettings["llm.session.model"] = selected.Model;
            await sessionStore.SaveAsync(session, ct);
        }
    }

    private static (string Provider, string Model) Validate(ChatCatalog catalog, string? provider, string? model)
    {
        var option = catalog.Providers.FirstOrDefault(item => item.Name.Equals(provider, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Provider '{provider}' is unavailable for chat.");
        var selectedModel = string.IsNullOrWhiteSpace(model) ? option.DefaultModel : model;
        if (string.IsNullOrWhiteSpace(selectedModel) ||
            !option.Models.Contains(selectedModel, StringComparer.OrdinalIgnoreCase) &&
            !string.Equals(option.DefaultModel, selectedModel, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Model '{selectedModel}' is unavailable for provider '{option.Name}'.");
        return (option.Name, selectedModel);
    }
}

public sealed record ChatProviderOption(string Name, string DefaultModel, IReadOnlyList<string> Models);
public sealed record ChatCatalog(string DefaultProvider, string DefaultModel,
    IReadOnlyList<ChatProviderOption> Providers, string? PreferredProvider, string? PreferredModel,
    bool CanManageTenant = false);
public sealed record UpdateChatSettingsRequest(string Provider, string Model);
