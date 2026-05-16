using System.Diagnostics;
using System.Globalization;
using AgenticSystem.Core.Interfaces;
using AgenticSystem.Core.LLM.Interfaces;
using AgenticSystem.Core.LLM.Models;
using AgenticSystem.Core.Models;
using AgenticSystem.Core.Services;
using AgenticSystem.Infrastructure.Configuration;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using MChatMessage = Microsoft.Extensions.AI.ChatMessage;
using Polly;

namespace AgenticSystem.Infrastructure.LLM;

public class LLMManager : ILLMAdministrationService
{
    private const string DefaultProviderConfigKey = "llm.default.provider";
    private const string DefaultModelConfigKey = "llm.default.model";
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, HashSet<string>> ProviderModelCatalog = new(StringComparer.OrdinalIgnoreCase)
    {
        ["OpenAI"] = new(StringComparer.OrdinalIgnoreCase) { "gpt-4o", "gpt-4o-mini" },
        ["Gemini"] = new(StringComparer.OrdinalIgnoreCase) { "gemini-1.5-pro", "gemini-1.5-flash", "gemini-2.0-flash-exp" },
        ["Claude"] = new(StringComparer.OrdinalIgnoreCase) { "claude-3-5-sonnet-latest", "claude-3-5-haiku-latest", "claude-3-opus-latest" },
        ["Ollama"] = new(StringComparer.OrdinalIgnoreCase) { "llama3", "llama3.1", "mistral", "qwen2.5" },
        ["OpenRouter"] = new(StringComparer.OrdinalIgnoreCase) { "openrouter/auto", "meta-llama/llama-3-8b-instruct", "google/gemini-2.5-flash", "anthropic/claude-3.5-sonnet" }
    };

    private readonly AgenticSystemSettings _settings;
    private readonly ILogger<LLMManager> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILLMRuntimeContextAccessor _llmRuntimeContextAccessor;
    private readonly ITenantStore _tenantStore;
    private readonly ISessionStore _sessionStore;
    private readonly IConfigManager? _configManager;
    private readonly IConfigReloadNotifier? _configReloadNotifier;
    private readonly IServiceProvider _serviceProvider;
    private readonly Dictionary<string, ILLMProvider> _providers = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IChatClient> _chatClientRegistry = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _configSync = new(1, 1);
    
    // Polly Resilience Policies per provider
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, (Polly.Wrap.AsyncPolicyWrap Policy, Polly.CircuitBreaker.AsyncCircuitBreakerPolicy CircuitBreaker)> _resiliencePolicies = new();

    private volatile bool _runtimeConfigDirty = true;
    private string? _defaultProviderOverride;
    private string? _defaultModelOverride;

    internal LLMManager(
        IEnumerable<ILLMProvider> providers,
        ILogger<LLMManager> logger,
        IServiceProvider serviceProvider)
    {
        _settings = new AgenticSystemSettings();
        _logger = logger;
        _loggerFactory = LoggerFactory.Create(_ => { });
        _llmRuntimeContextAccessor = new LLMRuntimeContextAccessor();
        _tenantStore = new InMemoryTenantStore();
        _sessionStore = new InMemorySessionStore();
        _serviceProvider = serviceProvider;
        _configManager = null;
        _configReloadNotifier = null;

        foreach (var provider in providers)
        {
            _providers[provider.Name] = provider;
            _chatClientRegistry[provider.Name] = BuildChatClient(provider);
        }

        _logger.LogInformation(
            "🧠 LLMManager initialized with {ProviderCount} providers ({Providers})",
            _providers.Count,
            string.Join(", ", _providers.Keys));
    }

    [ActivatorUtilitiesConstructor]
    public LLMManager(
        IOptions<AgenticSystemSettings> settingsOptions,
        ILogger<LLMManager> logger,
        ILoggerFactory loggerFactory,
        ILLMRuntimeContextAccessor llmRuntimeContextAccessor,
        ITenantStore tenantStore,
        ISessionStore sessionStore,
        IServiceProvider serviceProvider,
        IConfigManager? configManager = null,
        IConfigReloadNotifier? configReloadNotifier = null)
    {
        _settings = settingsOptions?.Value ?? new AgenticSystemSettings();
        _logger = logger;
        _loggerFactory = loggerFactory;
        _llmRuntimeContextAccessor = llmRuntimeContextAccessor;
        _tenantStore = tenantStore;
        _sessionStore = sessionStore;
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _configManager = configManager;
        _configReloadNotifier = configReloadNotifier;

        RegisterProvidersFromSettings(_settings);

        if (_configReloadNotifier is not null)
        {
            _configReloadNotifier.OnChange(key =>
            {
                if (key.StartsWith("llm.", StringComparison.OrdinalIgnoreCase))
                {
                    _runtimeConfigDirty = true;
                }
            });
        }

        _logger.LogInformation(
            "🧠 LLMManager initialized with {ProviderCount} providers ({Providers})",
            _providers.Count,
            string.Join(", ", _providers.Keys));
    }

    private (Polly.Wrap.AsyncPolicyWrap Policy, Polly.CircuitBreaker.AsyncCircuitBreakerPolicy CircuitBreaker) GetResiliencePolicy(string providerName)
    {
        return _resiliencePolicies.GetOrAdd(providerName, _ =>
        {
            var cb = Polly.Policy
                .Handle<Exception>(ex => 
                    ex.Message.Contains("429") || 
                    ex.Message.Contains("timeout") || 
                    ex is HttpRequestException || 
                    ex is TimeoutException)
                .CircuitBreakerAsync(
                    exceptionsAllowedBeforeBreaking: 3,
                    durationOfBreak: TimeSpan.FromSeconds(30),
                    onBreak: (ex, breakDelay) =>
                    {
                        _logger.LogWarning(ex, "🚨 Circuit Breaker for {Provider} tripped! Breaking for {Delay}s", providerName, breakDelay.TotalSeconds);
                    },
                    onReset: () =>
                    {
                        _logger.LogInformation("✅ Circuit Breaker for {Provider} reset.", providerName);
                    },
                    onHalfOpen: () =>
                    {
                        _logger.LogInformation("⏳ Circuit Breaker for {Provider} is half-open (testing next request).", providerName);
                    });

            var retry = Polly.Policy
                .Handle<Exception>(ex => 
                    ex.Message.Contains("429") || 
                    ex.Message.Contains("timeout") || 
                    ex is HttpRequestException || 
                    ex is TimeoutException)
                .WaitAndRetryAsync(
                    retryCount: 4,
                    sleepDurationProvider: (attempt) => 
                        TimeSpan.FromSeconds(Math.Pow(2, attempt)) + TimeSpan.FromMilliseconds(Random.Shared.Next(0, 1000)),
                    onRetry: (ex, delay, attempt, ctx) =>
                    {
                        _logger.LogWarning("⏳ Retrying {Provider} due to error (Attempt {Attempt}/4). Delaying for {Delay}ms. Error: {Message}", 
                            providerName, attempt, delay.TotalMilliseconds, ex.Message);
                    });

            return (Policy: Polly.Policy.WrapAsync(cb, retry), CircuitBreaker: cb);
        });
    }

    public async Task<LLMResponse> GenerateAsync(LLMRequest request, CancellationToken ct = default)
    {
        var selection = await ResolveSelectionAsync(request, ct);
        
        var messages = MapMessages(request);
        if (messages.Count == 0)
        {
            return LLMResponse.Fail("The request does not contain any prompt or chat messages.", selection.Provider);
        }

        var smartRouter = _serviceProvider.GetService<ISmartRouter>();
        ProviderRoutingDecision routingDecision = null!;
        if (smartRouter is not null)
        {
            routingDecision = await smartRouter.RouteProviderAsync(selection.Provider, selection.Model);
        }
        
        var candidates = new List<ProviderFallbackOption>
        {
            new ProviderFallbackOption { Provider = selection.Provider, Model = selection.Model }
        };

        if (routingDecision is not null && routingDecision.FallbackChain.Count > 0)
        {
            candidates.AddRange(routingDecision.FallbackChain);
        }

        var allEnabled = _providers.Values.Where(p => p.IsEnabled).OrderBy(p => p.Priority);
        foreach (var p in allEnabled)
        {
            if (!candidates.Any(c => c.Provider.Equals(p.Name, StringComparison.OrdinalIgnoreCase)))
            {
                candidates.Add(new ProviderFallbackOption { Provider = p.Name, Model = p.DefaultModel });
            }
        }

        var validator = _serviceProvider.GetService<IStructuredOutputValidator>();
        var maxAttempts = request.RequiredSchema != null ? request.MaxRetries : 1;
        var sw = Stopwatch.StartNew();
        var exceptions = new List<Exception>();

        foreach (var candidate in candidates)
        {
            if (exceptions.Count > 0)
            {
                _logger.LogWarning("🔄 Fallback initiated: Switching to fallback provider {NextProvider} ({Model}) after earlier failures.", candidate.Provider, candidate.Model);
            }

            var chatClient = await ResolveChatClientAsync(candidate.Provider, candidate.Model, selection.ApiKey, ct);
            var resilience = GetResiliencePolicy(candidate.Provider);

            // Verificação proativa de cotas antes do disparo
            var quotaService = _serviceProvider.GetService<IExternalQuotaSyncService>();
            if (quotaService != null && !await quotaService.IsProviderAvailableAsync(candidate.Provider))
            {
                _logger.LogWarning("⏭️ Skipping {Provider} (Quota Exceeded - Proactive check)", candidate.Provider);
                exceptions.Add(new InvalidOperationException($"Quota exceeded for {candidate.Provider}."));
                continue;
            }

            var circuitState = resilience.CircuitBreaker.CircuitState.ToString();
            if (circuitState == "Open" || circuitState == "Isolated")
            {
                _logger.LogWarning("⏭️ Skipping {Provider} (Circuit Open)", candidate.Provider);
                exceptions.Add(new InvalidOperationException($"Circuit breaker for {candidate.Provider} is Open."));
                continue;
            }

            var options = new ChatOptions
            {
                Temperature = (float?)request.Parameters.Temperature,
                MaxOutputTokens = request.Parameters.MaxTokens,
                TopP = (float?)request.Parameters.TopP,
                FrequencyPenalty = (float?)request.Parameters.FrequencyPenalty,
                PresencePenalty = (float?)request.Parameters.PresencePenalty,
                StopSequences = request.Parameters.Stop,
                ModelId = candidate.Model
            };
            
            // Ativa modo JSON (dependendo do modelo) se exigido
            if (request.ResponseFormat == ResponseFormat.Json || request.RequiredSchema != null)
            {
                options.ResponseFormat = ChatResponseFormat.Json;
            }

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    var response = await resilience.Policy.ExecuteAsync(async () => 
                        await chatClient.GetResponseAsync(messages, options, ct));

                    var responseText = response.Text ?? string.Empty;

                    if (request.RequiredSchema != null && validator != null)
                    {
                        var validationResult = await validator.ValidateAsync(responseText, request.RequiredSchema, ct);
                        if (!validationResult.IsValid)
                        {
                            _logger.LogWarning("⚠️ Schema validation failed on attempt {Attempt}/{MaxRetries} via {Provider}. Error: {Error}", attempt, maxAttempts, candidate.Provider, string.Join(", ", validationResult.ValidationErrors));
                            if (attempt < maxAttempts)
                            {
                                // Adiciona a falha como mensagem para a LLM corrigir
                                messages.Add(new MChatMessage(ChatRole.Assistant, responseText));
                                messages.Add(new MChatMessage(ChatRole.User, $"The JSON is invalid against the schema. Fix the errors: {string.Join("; ", validationResult.ValidationErrors)}"));
                                continue;
                            }
                            else
                            {
                                throw new Exception($"Schema validation failed after {maxAttempts} attempts.");
                            }
                        }
                    }

                    sw.Stop();

                    var llmResponse = LLMResponse.Ok(
                        responseText,
                        candidate.Model,
                        candidate.Provider,
                        MapUsage(response.Usage));

                    llmResponse.Latency = sw.Elapsed;

                    if (candidate.Provider != selection.Provider)
                    {
                        _logger.LogInformation("🔄 Fallback successful: switched from {Primary} to {Fallback}", selection.Provider, candidate.Provider);
                    }

                    return llmResponse;
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                    _logger.LogError(ex, "❌ Request via {Provider} failed on attempt {Attempt}.", candidate.Provider, attempt);
                    // Se a chamada falhar (por exemplo rede/timeout), não re-tenta localmente, avança para o próximo provedor.
                    break;
                }
            }
        }

        sw.Stop();
        
        var aggregateEx = new AggregateException("All fallback providers failed.", exceptions);
        var failedResponse = LLMResponse.Fail(aggregateEx.Message, selection.Provider);
        failedResponse.Latency = sw.Elapsed;
        return failedResponse;
    }

    public async Task<IReadOnlyList<LLMProviderInfo>> GetEnabledProvidersAsync(CancellationToken ct = default)
    {
        var configuration = await GetConfigurationAsync(ct);
        return configuration.Providers.Where(provider => provider.IsEnabled).ToList();
    }

    public async Task<LLMProviderInfo?> GetProviderAsync(string name, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var configuration = await GetConfigurationAsync(ct);
        return configuration.Providers.FirstOrDefault(provider =>
            provider.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<LLMProviderInfo?> GetDefaultProviderAsync(CancellationToken ct = default)
    {
        var configuration = await GetConfigurationAsync(ct);
        return configuration.Providers.FirstOrDefault(provider =>
            provider.Name.Equals(configuration.DefaultProvider, StringComparison.OrdinalIgnoreCase));
    }

    private ILLMProvider GetDefaultProvider()
    {
        var provider = TryGetDefaultProvider();
        if (provider is not null)
            return provider;

        throw new InvalidOperationException("No LLM providers are available.");
    }

    private ILLMProvider? TryGetDefaultProvider()
    {
        if (!string.IsNullOrWhiteSpace(_defaultProviderOverride)
            && _providers.TryGetValue(_defaultProviderOverride, out var configuredDefault)
            && configuredDefault.IsEnabled)
        {
            return configuredDefault;
        }

        var best = _providers.Values
            .Where(p => p.IsEnabled)
            .OrderBy(p => p.Priority)
            .FirstOrDefault();

        return best;
    }

    private async Task<IReadOnlyList<LLMProviderInfo>> GetAllProviderInfoAsync(CancellationToken ct = default)
    {
        var defaultProviderName = ResolveDefaultProviderName();
        var quotaService = _serviceProvider.GetService<IExternalQuotaSyncService>();
        var quotas = quotaService != null 
            ? await quotaService.GetAllQuotasAsync(null) // Global quotas
            : new List<ExternalProviderQuota>();

        var result = new List<LLMProviderInfo>();
        foreach (var p in _providers.Values.OrderBy(p => p.Priority))
        {
            var providerQuota = quotas.FirstOrDefault(q => q.ProviderName.Equals(p.Name, StringComparison.OrdinalIgnoreCase));
            
            result.Add(new LLMProviderInfo
            {
                Name = p.Name,
                DefaultModel = p.DefaultModel,
                IsEnabled = p.IsEnabled,
                Priority = p.Priority,
                HasApiKey = IsProviderConfigured(p),
                IsDefault = p.Name.Equals(defaultProviderName, StringComparison.OrdinalIgnoreCase),
                IsAvailable = p.IsEnabled && IsProviderConfigured(p),
                Models = BuildModelList(p.Name, p.DefaultModel),
                
                // New quota fields
                CurrentBalance = providerQuota?.BalanceRemaining,
                RequestsRemaining = providerQuota?.RemainingRequests,
                TokensRemaining = providerQuota?.RemainingTokens,
                QuotaExceeded = providerQuota?.IsExhausted ?? false,
                LastQuotaUpdate = providerQuota?.LastSyncAt
            });
        }
        
        return result;
    }

    public async Task<LLMConfigurationInfo> GetConfigurationAsync(CancellationToken ct = default)
    {
        await EnsureRuntimeConfigLoadedAsync(ct);

        var defaultProvider = TryGetDefaultProvider();
        var fallbackProvider = defaultProvider
            ?? _providers.Values.OrderBy(p => p.Priority).FirstOrDefault();

        return new LLMConfigurationInfo
        {
            DefaultProvider = ResolveDefaultProviderName(),
            DefaultModel = fallbackProvider is not null
                ? ResolveDefaultModel(fallbackProvider)
                : _defaultModelOverride ?? string.Empty,
            Providers = await GetAllProviderInfoAsync(ct)
        };
    }

    public async Task<bool> TestProviderAsync(string name, CancellationToken ct = default)
    {
        if (!_providers.TryGetValue(name, out var provider))
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

    public async Task<LLMProviderInfo?> UpdateProviderAsync(string name, UpdateProviderRequest request, CancellationToken ct = default)
    {
        if (!_providers.TryGetValue(name, out var provider))
            return null;

        if (request.DiscoveredModels is not null && request.DiscoveredModels.Count > 0)
        {
            var known = ProviderModelCatalog.GetOrAdd(name, _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            foreach (var m in request.DiscoveredModels)
            {
                if (!string.IsNullOrWhiteSpace(m))
                    known.Add(m.Trim());
            }
        }

        provider.Configure(request.ApiKey, request.DefaultModel, request.Enabled, request.Priority);
        _chatClientRegistry[name] = BuildChatClient(provider);
        await PersistProviderConfigurationAsync(name, request, provider);

        _logger.LogInformation(
            "✅ Provider '{Provider}' updated — Enabled={Enabled}, Priority={Priority}, Model={Model}",
            name,
            provider.IsEnabled,
            provider.Priority,
            provider.DefaultModel);

        return await GetProviderAsync(name, ct);
    }

    public async Task<DiscoverModelsResponse> DiscoverModelsAsync(string name, DiscoverModelsRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.ApiKey))
        {
            return new DiscoverModelsResponse { Success = false, ErrorMessage = "A chave de API é obrigatória." };
        }

        var discovered = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };

            if (name.Equals("OpenAI", StringComparison.OrdinalIgnoreCase))
            {
                httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", request.ApiKey);
                using var response = await httpClient.GetAsync("https://api.openai.com/v1/models", ct);
                if (!response.IsSuccessStatusCode)
                {
                    var errorText = await response.Content.ReadAsStringAsync(ct);
                    return new DiscoverModelsResponse { Success = false, ErrorMessage = $"Falha ao consultar OpenAI ({response.StatusCode}): {errorText}" };
                }

                using var doc = await System.Text.Json.JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
                if (doc.RootElement.TryGetProperty("data", out var dataProp) && dataProp.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    foreach (var item in dataProp.EnumerateArray())
                    {
                        if (item.TryGetProperty("id", out var idProp) && idProp.ValueKind == System.Text.Json.JsonValueKind.String)
                        {
                            var modelId = idProp.GetString();
                            if (!string.IsNullOrWhiteSpace(modelId))
                            {
                                discovered.Add(modelId);
                            }
                        }
                    }
                }
            }
            else if (name.Equals("Gemini", StringComparison.OrdinalIgnoreCase))
            {
                var url = $"https://generativelanguage.googleapis.com/v1beta/models?key={request.ApiKey}";
                using var response = await httpClient.GetAsync(url, ct);
                if (!response.IsSuccessStatusCode)
                {
                    var errorText = await response.Content.ReadAsStringAsync(ct);
                    return new DiscoverModelsResponse { Success = false, ErrorMessage = $"Falha ao consultar Gemini ({response.StatusCode}): {errorText}" };
                }

                using var doc = await System.Text.Json.JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
                if (doc.RootElement.TryGetProperty("models", out var modelsProp) && modelsProp.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    foreach (var item in modelsProp.EnumerateArray())
                    {
                        bool isSupported = false;
                        if (item.TryGetProperty("supportedGenerationMethods", out var methodsProp) && methodsProp.ValueKind == System.Text.Json.JsonValueKind.Array)
                        {
                            foreach (var m in methodsProp.EnumerateArray())
                            {
                                if (m.ValueKind == System.Text.Json.JsonValueKind.String && m.GetString() == "generateContent")
                                {
                                    isSupported = true;
                                    break;
                                }
                            }
                        }

                        if (isSupported && item.TryGetProperty("name", out var nameProp) && nameProp.ValueKind == System.Text.Json.JsonValueKind.String)
                        {
                            var modelName = nameProp.GetString();
                            if (!string.IsNullOrWhiteSpace(modelName))
                            {
                                if (modelName.StartsWith("models/"))
                                {
                                    modelName = modelName.Substring("models/".Length);
                                }
                                discovered.Add(modelName);
                            }
                        }
                    }
                }
            }
            else if (name.Equals("Claude", StringComparison.OrdinalIgnoreCase))
            {
                httpClient.DefaultRequestHeaders.Add("x-api-key", request.ApiKey);
                httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
                using var response = await httpClient.GetAsync("https://api.anthropic.com/v1/models", ct);
                if (!response.IsSuccessStatusCode)
                {
                    var errorText = await response.Content.ReadAsStringAsync(ct);
                    return new DiscoverModelsResponse { Success = false, ErrorMessage = $"Falha ao consultar Claude ({response.StatusCode}): {errorText}" };
                }

                using var doc = await System.Text.Json.JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
                if (doc.RootElement.TryGetProperty("data", out var dataProp) && dataProp.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    foreach (var item in dataProp.EnumerateArray())
                    {
                        if (item.TryGetProperty("id", out var idProp) && idProp.ValueKind == System.Text.Json.JsonValueKind.String)
                        {
                            var modelId = idProp.GetString();
                            if (!string.IsNullOrWhiteSpace(modelId))
                            {
                                discovered.Add(modelId);
                            }
                        }
                    }
                }
            }
            else if (name.Equals("OpenRouter", StringComparison.OrdinalIgnoreCase))
            {
                httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", request.ApiKey);
                using var response = await httpClient.GetAsync("https://openrouter.ai/api/v1/models", ct);
                if (!response.IsSuccessStatusCode)
                {
                    var errorText = await response.Content.ReadAsStringAsync(ct);
                    return new DiscoverModelsResponse { Success = false, ErrorMessage = $"Falha ao consultar OpenRouter ({response.StatusCode}): {errorText}" };
                }

                using var doc = await System.Text.Json.JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
                if (doc.RootElement.TryGetProperty("data", out var dataProp) && dataProp.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    foreach (var item in dataProp.EnumerateArray())
                    {
                        if (item.TryGetProperty("id", out var idProp) && idProp.ValueKind == System.Text.Json.JsonValueKind.String)
                        {
                            var modelId = idProp.GetString();
                            if (!string.IsNullOrWhiteSpace(modelId))
                            {
                                discovered.Add(modelId);
                            }
                        }
                    }
                }
            }

            // Fazer o merge com os modelos do catálogo inicial para garantir que não perdemos nenhum fallback importante
            if (ProviderModelCatalog.TryGetValue(name, out var existingCatalog))
            {
                foreach (var m in existingCatalog) discovered.Add(m);
            }

            return new DiscoverModelsResponse
            {
                Success = true,
                DiscoveredModels = discovered.ToList()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Erro inesperado ao descobrir modelos do provedor {Provider}.", name);
            return new DiscoverModelsResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    public async Task SyncQuotasAsync(CancellationToken ct = default)
    {
        await EnsureRuntimeConfigLoadedAsync(ct);
        var quotaService = _serviceProvider.GetService<IExternalQuotaSyncService>();
        if (quotaService == null) return;

        foreach (var p in _providers.Values)
        {
            if (!p.IsEnabled) continue;
            
            // For config-based keys (infrastructure-global)
            var prefix = GetProviderConfigPrefix(p.Name);
            var apiKey = _configManager != null 
                ? await _configManager.ResolveValueAsync($"{prefix}.apiKey")
                : null;
            
            if (string.IsNullOrWhiteSpace(apiKey)) continue;

            // Trigger sync
            await quotaService.SyncBillingAsync(p.Name, null, "infrastructure-global", apiKey);
        }
    }

    public async Task<LLMConfigurationInfo> UpdateDefaultSelectionAsync(UpdateDefaultLlmSelectionRequest request, CancellationToken ct = default)
    {
        await EnsureRuntimeConfigLoadedAsync(ct);

        if (!_providers.TryGetValue(request.ProviderName, out var provider))
            throw new InvalidOperationException($"Provider '{request.ProviderName}' not found.");

        if (!provider.IsEnabled)
            throw new InvalidOperationException($"Provider '{request.ProviderName}' is disabled.");

        var model = string.IsNullOrWhiteSpace(request.Model)
            ? provider.DefaultModel
            : request.Model;

        _defaultProviderOverride = provider.Name;
        _defaultModelOverride = model;
        _runtimeConfigDirty = false;

        if (_configManager is not null)
        {
            await UpsertConfigEntryAsync(
                DefaultProviderConfigKey,
                provider.Name,
                isSecret: false,
                ConfigCategory.Provider,
                provider.Name,
                "Provider default utilizado no chat.");

            await UpsertConfigEntryAsync(
                DefaultModelConfigKey,
                model,
                isSecret: false,
                ConfigCategory.Provider,
                provider.Name,
                "Modelo default utilizado no chat.");
        }

        return await GetConfigurationAsync(ct);
    }

    internal async Task<(IChatClient ChatClient, string ResolvedModel)> ResolveChatClientForCurrentContextAsync(
        string? requestedModel,
        CancellationToken ct = default)
    {
        var selection = await ResolveSelectionAsync(new LLMRequest { Model = requestedModel }, ct);
        var chatClient = await ResolveChatClientAsync(selection.Provider, selection.Model, selection.ApiKey, ct);
        return (chatClient, selection.Model);
    }

    internal async Task<IReadOnlyList<(IChatClient ChatClient, string ResolvedModel, string ProviderName)>> GetFallbackChatClientsAsync(
        string? requestedModel,
        CancellationToken ct = default)
    {
        var selection = await ResolveSelectionAsync(new LLMRequest { Model = requestedModel }, ct);
        var list = new List<(IChatClient, string, string)>();

        var primaryClient = await ResolveChatClientAsync(selection.Provider, selection.Model, selection.ApiKey, ct);
        list.Add((primaryClient, selection.Model, selection.Provider));

        var smartRouter = _serviceProvider.GetService<ISmartRouter>();
        if (smartRouter is not null)
        {
            var decision = await smartRouter.RouteProviderAsync(selection.Provider, selection.Model);
            if (decision is not null && decision.FallbackChain.Count > 0)
            {
                foreach (var opt in decision.FallbackChain)
                {
                    if (!opt.Provider.Equals(selection.Provider, StringComparison.OrdinalIgnoreCase))
                    {
                        var fallbackClient = await ResolveChatClientAsync(opt.Provider, opt.Model, selection.ApiKey, ct);
                        list.Add((fallbackClient, opt.Model, opt.Provider));
                    }
                }
            }
        }

        var allEnabled = _providers.Values.Where(p => p.IsEnabled).OrderBy(p => p.Priority);
        foreach (var p in allEnabled)
        {
            if (!list.Any(x => x.Item3.Equals(p.Name, StringComparison.OrdinalIgnoreCase)))
            {
                var fallbackClient = await ResolveChatClientAsync(p.Name, p.DefaultModel, selection.ApiKey, ct);
                list.Add((fallbackClient, p.DefaultModel, p.Name));
            }
        }

        return list;
    }

    private async Task EnsureRuntimeConfigLoadedAsync(CancellationToken ct)
    {
        if (!_runtimeConfigDirty || _configManager is null)
            return;

        await _configSync.WaitAsync(ct);
        try
        {
            if (!_runtimeConfigDirty)
                return;

            var defaultProvider = await _configManager.ResolveValueAsync(DefaultProviderConfigKey);
            if (!string.IsNullOrWhiteSpace(defaultProvider) && _providers.ContainsKey(defaultProvider))
                _defaultProviderOverride = defaultProvider;

            var defaultModel = await _configManager.ResolveValueAsync(DefaultModelConfigKey);
            _defaultModelOverride = string.IsNullOrWhiteSpace(defaultModel) ? null : defaultModel;

            foreach (var provider in _providers.Values)
            {
                var prefix = GetProviderConfigPrefix(provider.Name);
                var apiKey = await _configManager.ResolveValueAsync($"{prefix}.apiKey");
                var model = await _configManager.ResolveValueAsync($"{prefix}.model");
                var enabledRaw = await _configManager.ResolveValueAsync($"{prefix}.enabled");
                var priorityRaw = await _configManager.ResolveValueAsync($"{prefix}.priority");
                var modelsRaw = await _configManager.ResolveValueAsync($"{prefix}.models");

                if (!string.IsNullOrWhiteSpace(modelsRaw))
                {
                    var known = ProviderModelCatalog.GetOrAdd(provider.Name, _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase));
                    foreach (var m in modelsRaw.Split(',', StringSplitOptions.RemoveEmptyEntries))
                    {
                        known.Add(m.Trim());
                    }
                }

                bool? enabled = TryParseBool(enabledRaw);
                int? priority = TryParseInt(priorityRaw);

                provider.Configure(apiKey, model, enabled, priority);
                _chatClientRegistry[provider.Name] = BuildChatClient(provider);
            }

            _runtimeConfigDirty = false;
        }
        finally
        {
            _configSync.Release();
        }
    }

    private async Task<ResolvedSelection> ResolveSelectionAsync(LLMRequest request, CancellationToken ct)
    {
        await EnsureRuntimeConfigLoadedAsync(ct);

        var runtime = _llmRuntimeContextAccessor.Current;
        var session = await ResolveSessionAsync(runtime?.SessionId, ct);
        var tenant = await ResolveTenantAsync(runtime?.TenantId, ct);

        var requestProvider = FirstNonEmpty(request.Provider, runtime?.RequestProvider);
        var sessionProvider = FirstNonEmpty(runtime?.SessionProvider, ReadSessionSetting(session, "llm.session.provider"));
        var tenantProvider = ReadTenantSetting(tenant, "llm.provider");

        var providerName = FirstNonEmpty(
            requestProvider,
            sessionProvider,
            tenantProvider,
            _defaultProviderOverride,
            GetDefaultProvider().Name)
            ?? GetDefaultProvider().Name;

        var provider = _providers.TryGetValue(providerName, out var selected)
            ? selected
            : GetDefaultProvider();

        var requestModel = FirstNonEmpty(request.Model, runtime?.RequestModel);
        var sessionModel = FirstNonEmpty(runtime?.SessionModel, ReadSessionSetting(session, "llm.session.model"));
        var tenantModel = ReadTenantSetting(tenant, "llm.model");

        var model = FirstNonEmpty(
            requestModel,
            sessionModel,
            tenantModel,
            _defaultModelOverride,
            provider.DefaultModel)
            ?? provider.DefaultModel;

        var requestApiKey = runtime?.RequestApiKey;
        var sessionApiKey = FirstNonEmpty(runtime?.SessionApiKey, ReadSessionSetting(session, "llm.session.apiKey"));
        var tenantApiKey = ReadTenantApiKey(tenant, provider.Name);
        var configApiKey = _configManager is null
            ? null
            : await _configManager.ResolveValueAsync($"{GetProviderConfigPrefix(provider.Name)}.apiKey");

        var apiKey = FirstNonEmpty(requestApiKey, sessionApiKey, tenantApiKey, configApiKey);

        return new ResolvedSelection(provider.Name, model, apiKey);
    }

    private async Task<IChatClient> ResolveChatClientAsync(string providerName, string model, string? apiKey, CancellationToken ct)
    {
        await EnsureRuntimeConfigLoadedAsync(ct);

        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            var ephemeralProvider = CreateEphemeralProvider(providerName, apiKey, model);
            return BuildChatClient(ephemeralProvider);
        }

        if (_chatClientRegistry.TryGetValue(providerName, out var client))
            return client;

        var fallback = GetDefaultProvider();
        return _chatClientRegistry[fallback.Name];
    }

    private static string? FirstNonEmpty(params string?[] candidates)
    {
        foreach (var candidate in candidates)
        {
            if (!string.IsNullOrWhiteSpace(candidate))
                return candidate;
        }

        return null;
    }

    private static bool? TryParseBool(string? raw)
    {
        if (bool.TryParse(raw, out var parsed))
            return parsed;

        return null;
    }

    private static int? TryParseInt(string? raw)
    {
        if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            return parsed;

        return null;
    }

    private static string GetProviderConfigPrefix(string providerName)
        => $"llm.providers.{providerName.ToLowerInvariant()}";

    private string ResolveDefaultProviderName()
    {
        if (!string.IsNullOrWhiteSpace(_defaultProviderOverride)
            && _providers.ContainsKey(_defaultProviderOverride))
        {
            return _defaultProviderOverride;
        }

        return TryGetDefaultProvider()?.Name
            ?? _providers.Values.OrderBy(p => p.Priority).FirstOrDefault()?.Name
            ?? string.Empty;
    }

    private string ResolveDefaultModel(ILLMProvider provider)
        => string.IsNullOrWhiteSpace(_defaultModelOverride) ? provider.DefaultModel : _defaultModelOverride;

    private static IReadOnlyList<string> BuildModelList(string providerName, string defaultModel)
    {
        var models = ProviderModelCatalog.TryGetValue(providerName, out var knownModels)
            ? new List<string>(knownModels)
            : new List<string>();

        if (!string.IsNullOrWhiteSpace(defaultModel) && !models.Contains(defaultModel, StringComparer.OrdinalIgnoreCase))
        {
            models.Insert(0, defaultModel);
        }

        return models;
    }

    private async Task PersistProviderConfigurationAsync(string providerName, UpdateProviderRequest request, ILLMProvider provider)
    {
        if (_configManager is null)
            return;

        var prefix = GetProviderConfigPrefix(providerName);

        if (request.ApiKey is not null)
        {
            await UpsertConfigEntryAsync(
                $"{prefix}.apiKey",
                request.ApiKey,
                isSecret: true,
                ConfigCategory.Credentials,
                providerName,
                $"API key do provider {providerName}.");
        }

        if (!string.IsNullOrWhiteSpace(request.DefaultModel))
        {
            await UpsertConfigEntryAsync(
                $"{prefix}.model",
                provider.DefaultModel,
                isSecret: false,
                ConfigCategory.Provider,
                providerName,
                $"Modelo default do provider {providerName}.");
        }

        if (request.Enabled.HasValue)
        {
            await UpsertConfigEntryAsync(
                $"{prefix}.enabled",
                provider.IsEnabled.ToString(),
                isSecret: false,
                ConfigCategory.Provider,
                providerName,
                $"Flag de habilitação do provider {providerName}.");
        }

        if (request.Priority.HasValue)
        {
            await UpsertConfigEntryAsync(
                $"{prefix}.priority",
                provider.Priority.ToString(CultureInfo.InvariantCulture),
                isSecret: false,
                ConfigCategory.Provider,
                providerName,
                $"Prioridade de roteamento do provider {providerName}.");
        }

        if (request.DiscoveredModels is not null && request.DiscoveredModels.Count > 0)
        {
            await UpsertConfigEntryAsync(
                $"{prefix}.models",
                string.Join(",", request.DiscoveredModels),
                isSecret: false,
                ConfigCategory.Provider,
                providerName,
                $"Modelos descobertos do provider {providerName}.");
        }
    }

    private async Task UpsertConfigEntryAsync(
        string key,
        string value,
        bool isSecret,
        ConfigCategory category,
        string? provider,
        string description)
    {
        if (_configManager is null)
            return;

        var request = new ConfigEntryRequest
        {
            Key = key,
            Value = value,
            IsSecret = isSecret,
            Category = category,
            Provider = provider,
            Description = description
        };

        try
        {
            await _configManager.GetAsync(key);
            await _configManager.UpdateAsync(key, request);
        }
        catch (KeyNotFoundException)
        {
            await _configManager.SetAsync(request);
        }
    }

    private static string? ReadSessionSetting(SessionData? session, string key)
    {
        if (session?.RuntimeSettings is null)
            return null;

        if (!session.RuntimeSettings.TryGetValue(key, out var value))
            return null;

        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string? ReadTenantApiKey(Tenant? tenant, string providerName)
    {
        if (tenant is null)
            return null;

        if (!tenant.ProviderApiKeys.TryGetValue(providerName, out var value))
            return null;

        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string? ReadTenantSetting(Tenant? tenant, string key)
    {
        if (tenant?.Settings is null)
            return null;

        if (!tenant.Settings.TryGetValue(key, out var value) || value is null)
            return null;

        var text = value.ToString();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private async Task<SessionData?> ResolveSessionAsync(string? sessionId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            return null;

        return await _sessionStore.GetAsync(sessionId, ct);
    }

    private async Task<Tenant?> ResolveTenantAsync(string? tenantId, CancellationToken ct)
    {
        var normalizedTenantId = string.IsNullOrWhiteSpace(tenantId) ? Tenant.DefaultTenantId : tenantId;
        return await _tenantStore.GetByIdAsync(normalizedTenantId, ct);
    }

    private ILLMProvider CreateEphemeralProvider(string providerName, string apiKey, string model)
    {
        if (!_providers.TryGetValue(providerName, out var existingProvider))
            existingProvider = GetDefaultProvider();

        return providerName.ToLowerInvariant() switch
        {
            "openai" or "gpt" => CreateOpenAiProvider(new OpenAISettings
            {
                ApiKey = apiKey,
                BaseUrl = _settings.OpenAI.BaseUrl,
                DefaultModel = model,
                Enabled = true,
                Priority = existingProvider.Priority
            }),
            "gemini" => CreateGeminiProvider(new GeminiSettings
            {
                ApiKey = apiKey,
                BaseUrl = _settings.Gemini.BaseUrl,
                DefaultModel = model,
                Enabled = true,
                Priority = existingProvider.Priority
            }),
            "claude" => CreateClaudeProvider(new ClaudeSettings
            {
                ApiKey = apiKey,
                BaseUrl = _settings.Claude.BaseUrl,
                DefaultModel = model,
                Enabled = true,
                Priority = existingProvider.Priority
            }),
            "openrouter" => CreateOpenRouterProvider(new OpenRouterSettings
            {
                ApiKey = apiKey,
                BaseUrl = _settings.OpenRouter.BaseUrl,
                DefaultModel = model,
                Enabled = true,
                Priority = existingProvider.Priority
            }),
            _ => existingProvider
        };
    }

    private void RegisterProvidersFromSettings(AgenticSystemSettings settings)
    {
        if (settings is null) return;

        if (_serviceProvider is null)
        {
            throw new InvalidOperationException("_serviceProvider is null in RegisterProvidersFromSettings");
        }

        RegisterProvider(CreateOpenAiProvider(settings.OpenAI));
        RegisterProvider(CreateGeminiProvider(settings.Gemini));
        RegisterProvider(CreateClaudeProvider(settings.Claude));
        RegisterProvider(CreateOpenRouterProvider(settings.OpenRouter));
        RegisterProvider(CreateOllamaProvider(settings.Ollama));
    }

    private void RegisterProvider(ILLMProvider provider)
    {
        _providers[provider.Name] = provider;
        _chatClientRegistry[provider.Name] = BuildChatClient(provider);
    }

    private IChatClient BuildChatClient(ILLMProvider provider)
    {
        var resilience = GetResiliencePolicy(provider.Name);
        return new ProviderBackedChatClient(provider, resilience.Policy);
    }

    private bool IsProviderConfigured(ILLMProvider provider)
    {
        return provider.Name.Equals("Ollama", StringComparison.OrdinalIgnoreCase)
            || provider.IsEnabled;
    }

    private ILLMProvider CreateOpenAiProvider(OpenAISettings settings)
    {
        var httpClient = _serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient(typeof(OpenAIProvider).FullName!);
        return new OpenAIProvider(
            httpClient,
            Options.Create(settings),
            _loggerFactory.CreateLogger<OpenAIProvider>());
    }

    private ILLMProvider CreateGeminiProvider(GeminiSettings settings)
    {
        var httpClient = _serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient(typeof(GeminiProvider).FullName!);
        return new GeminiProvider(
            httpClient,
            Options.Create(settings),
            _loggerFactory.CreateLogger<GeminiProvider>());
    }

    private ILLMProvider CreateClaudeProvider(ClaudeSettings settings)
    {
        var httpClient = _serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient(typeof(ClaudeProvider).FullName!);
        return new ClaudeProvider(
            httpClient,
            Options.Create(settings),
            _loggerFactory.CreateLogger<ClaudeProvider>());
    }

    private ILLMProvider CreateOpenRouterProvider(OpenRouterSettings settings)
    {
        var httpClient = _serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient(typeof(OpenRouterProvider).FullName!);
        return new OpenRouterProvider(
            httpClient,
            Options.Create(settings),
            _loggerFactory.CreateLogger<OpenRouterProvider>());
    }

    private ILLMProvider CreateOllamaProvider(OllamaSettings settings)
    {
        var httpClient = _serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient(typeof(OllamaProvider).FullName!);
        return new OllamaProvider(
            httpClient,
            Options.Create(settings),
            _loggerFactory.CreateLogger<OllamaProvider>());
    }

    private static List<MChatMessage> MapMessages(LLMRequest request)
    {
        var messages = new List<MChatMessage>();

        if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
        {
            messages.Add(new MChatMessage(ChatRole.System, request.SystemPrompt));
        }

        if (request.Messages.Count > 0)
        {
            foreach (var message in request.Messages)
            {
                var role = message.Role.ToLowerInvariant() switch
                {
                    "system" => ChatRole.System,
                    "assistant" => ChatRole.Assistant,
                    _ => ChatRole.User
                };

                messages.Add(new MChatMessage(role, message.Content));
            }

            return messages;
        }

        if (!string.IsNullOrWhiteSpace(request.Prompt))
        {
            messages.Add(new MChatMessage(ChatRole.User, request.Prompt));
        }

        return messages;
    }

    private static UsageInfo MapUsage(UsageDetails? usage)
    {
        if (usage is null)
            return new UsageInfo();

        return new UsageInfo
        {
            PromptTokens = (int)(usage.InputTokenCount ?? 0),
            CompletionTokens = (int)(usage.OutputTokenCount ?? 0)
        };
    }

    private sealed record ResolvedSelection(string Provider, string Model, string? ApiKey);
}
