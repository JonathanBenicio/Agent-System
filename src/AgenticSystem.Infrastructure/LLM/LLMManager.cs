using Microsoft.Extensions.Logging;
using AgenticSystem.Core.LLM.Interfaces;
using AgenticSystem.Core.LLM.Models;

namespace AgenticSystem.Infrastructure.LLM;

public class LLMManager : ILLMManager
{
    private readonly Dictionary<string, ILLMProvider> _providers;
    private readonly Dictionary<string, ILLMProvider> _allProviders;
    private readonly Dictionary<string, AgentLLMProfile> _profiles;
    private readonly ILogger<LLMManager> _logger;

    public LLMManager(IEnumerable<ILLMProvider> providers, ILogger<LLMManager> logger)
    {
        var providerList = providers.ToList();
        _allProviders = providerList.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);
        _providers = providerList
            .Where(p => p.IsEnabled)
            .ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);
        _profiles = new Dictionary<string, AgentLLMProfile>(StringComparer.OrdinalIgnoreCase);
        _logger = logger;

        _logger.LogInformation("🧠 LLMManager initialized with {Count} provider(s): {Providers}",
            _providers.Count, string.Join(", ", _providers.Keys));
    }

    public async Task<LLMResponse> GenerateAsync(LLMRequest request, CancellationToken ct = default)
    {
        var provider = !string.IsNullOrWhiteSpace(request.Provider)
            ? GetProvider(request.Provider)
            : GetDefaultProvider();

        return await provider.GenerateAsync(request, ct);
    }

    public async Task<LLMResponse> GenerateWithProfileAsync(string agentName, string taskType, string prompt, CancellationToken ct = default)
    {
        var profile = GetProfileOrDefault(agentName);
        var provider = !string.IsNullOrWhiteSpace(profile.PreferredProvider)
            ? GetProvider(profile.PreferredProvider)
            : GetDefaultProvider();

        var parameters = profile.GetParametersForTask(taskType);
        var request = new LLMRequest
        {
            Prompt = prompt,
            Model = profile.PreferredModel,
            Parameters = parameters
        };

        return await provider.GenerateAsync(request, ct);
    }

    public ILLMProvider GetProvider(string name)
    {
        if (_providers.TryGetValue(name, out var provider))
            return provider;

        _logger.LogWarning("⚠️ Provider '{Provider}' not found, falling back to default", name);
        return GetDefaultProvider();
    }

    public ILLMProvider GetDefaultProvider()
    {
        var provider = _providers.Values
            .OrderBy(p => p.Priority)
            .FirstOrDefault();

        return provider ?? throw new InvalidOperationException("No LLM providers are available.");
    }

    public IEnumerable<ILLMProvider> GetEnabledProviders() => _providers.Values;

    public IEnumerable<LLMProviderInfo> GetAllProviderInfo()
    {
        return _allProviders.Values.Select(p => new LLMProviderInfo
        {
            Name = p.Name,
            DefaultModel = p.DefaultModel,
            IsEnabled = p.IsEnabled,
            Priority = p.Priority,
            HasApiKey = p.IsEnabled
        });
    }

    public async Task<bool> TestProviderAsync(string name, CancellationToken ct = default)
    {
        if (!_allProviders.TryGetValue(name, out var provider))
            return false;

        try
        {
            return await provider.IsAvailableAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "⚠️ Provider '{Provider}' test failed", name);
            return false;
        }
    }

    public bool UpdateProvider(string name, UpdateProviderRequest request)
    {
        if (!_allProviders.TryGetValue(name, out var provider))
            return false;

        provider.Configure(request.ApiKey, request.DefaultModel, request.Enabled, request.Priority);

        if (provider.IsEnabled)
            _providers[name] = provider;
        else
            _providers.Remove(name);

        _logger.LogInformation("✅ Provider '{Provider}' updated — Enabled={Enabled}, Priority={Priority}, Model={Model}",
            name, provider.IsEnabled, provider.Priority, provider.DefaultModel);

        return true;
    }

    public void RegisterProfile(AgentLLMProfile profile)
    {
        _profiles[profile.AgentName] = profile;
    }

    private AgentLLMProfile GetProfileOrDefault(string agentName)
    {
        if (_profiles.TryGetValue(agentName, out var profile))
            return profile;

        return new AgentLLMProfile
        {
            AgentName = agentName,
            PreferredModel = GetDefaultProvider().DefaultModel
        };
    }
}
