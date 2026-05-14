using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Infrastructure.LLM;

/// <summary>
/// Proxy de IChatClient que resolve provider/model dinamicamente
/// com base no contexto runtime atual (request/sessao/tenant).
/// </summary>
public sealed class ContextAwareChatClient : IChatClient
{
    private readonly LLMManager _llmManager;
    private readonly ILogger<ContextAwareChatClient> _logger;
    private bool _disposed;

    public ContextAwareChatClient(LLMManager llmManager, ILogger<ContextAwareChatClient> logger)
    {
        _llmManager = llmManager;
        _logger = logger;
    }

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var clients = await _llmManager.GetFallbackChatClientsAsync(options?.ModelId, cancellationToken);
        var exceptions = new List<Exception>();

        foreach (var (chatClient, resolvedModel, providerName) in clients)
        {
            try
            {
                var effectiveOptions = BuildEffectiveOptions(options, resolvedModel);
                var response = await chatClient.GetResponseAsync(messages, effectiveOptions, cancellationToken);
                
                if (exceptions.Count > 0)
                {
                    _logger.LogInformation("🔄 Fallback successful: switched to fallback provider {Provider} ({Model}) after earlier failures.", providerName, resolvedModel);
                }
                return response;
            }
            catch (Exception ex) when (ex.GetType().FullName == "Polly.CircuitBreaker.BrokenCircuitException" || ex.Message.Contains("429") || ex is HttpRequestException || ex is TimeoutException)
            {
                exceptions.Add(ex);
                _logger.LogWarning(ex, "🚨 Provider {Provider} failed or circuit is open. Attempting next fallback provider.", providerName);
            }
        }

        throw new AggregateException("All LLM providers (including fallbacks) failed or have open circuits.", exceptions);
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var clients = await _llmManager.GetFallbackChatClientsAsync(options?.ModelId, cancellationToken);
        var exceptions = new List<Exception>();
        bool success = false;

        foreach (var (chatClient, resolvedModel, providerName) in clients)
        {
            IAsyncEnumerator<ChatResponseUpdate>? enumerator = null;
            ChatResponseUpdate? firstUpdate = null;
            try
            {
                var effectiveOptions = BuildEffectiveOptions(options, resolvedModel);
                enumerator = chatClient.GetStreamingResponseAsync(messages, effectiveOptions, cancellationToken).GetAsyncEnumerator(cancellationToken);
                if (await enumerator.MoveNextAsync())
                {
                    firstUpdate = enumerator.Current;
                    success = true;
                    if (exceptions.Count > 0)
                    {
                        _logger.LogInformation("🔄 Fallback streaming successful: switched to fallback provider {Provider} ({Model}) after earlier failures.", providerName, resolvedModel);
                    }
                }
            }
            catch (Exception ex) when (ex.GetType().FullName == "Polly.CircuitBreaker.BrokenCircuitException" || ex.Message.Contains("429") || ex is HttpRequestException || ex is TimeoutException)
            {
                if (enumerator is IAsyncDisposable disp)
                {
                    await disp.DisposeAsync();
                }
                exceptions.Add(ex);
                _logger.LogWarning(ex, "🚨 Provider {Provider} streaming failed or circuit is open. Attempting next fallback provider.", providerName);
                continue;
            }

            if (success && enumerator != null && firstUpdate != null)
            {
                try
                {
                    yield return firstUpdate;
                    while (await enumerator.MoveNextAsync())
                    {
                        yield return enumerator.Current;
                    }
                }
                finally
                {
                    if (enumerator is IAsyncDisposable disp)
                    {
                        await disp.DisposeAsync();
                    }
                }
                yield break;
            }
        }

        throw new AggregateException("All LLM providers (including fallbacks) failed streaming or have open circuits.", exceptions);
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        ArgumentNullException.ThrowIfNull(serviceType);

        if (serviceType == typeof(IChatClient))
            return this;

        return null;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
    }

    private static ChatOptions BuildEffectiveOptions(ChatOptions? source, string resolvedModel)
    {
        if (source is null)
        {
            return new ChatOptions { ModelId = resolvedModel };
        }

        if (string.IsNullOrWhiteSpace(source.ModelId))
        {
            source.ModelId = resolvedModel;
        }

        return source;
    }
}
