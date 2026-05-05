using Microsoft.Extensions.AI;

namespace AgenticSystem.Infrastructure.LLM;

/// <summary>
/// Proxy de IChatClient que resolve provider/model dinamicamente
/// com base no contexto runtime atual (request/sessao/tenant).
/// </summary>
public sealed class ContextAwareChatClient : IChatClient
{
    private readonly LLMManager _llmManager;
    private bool _disposed;

    public ContextAwareChatClient(LLMManager llmManager)
    {
        _llmManager = llmManager;
    }

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var (chatClient, resolvedModel) = await _llmManager.ResolveChatClientForCurrentContextAsync(options?.ModelId, cancellationToken);

        var effectiveOptions = BuildEffectiveOptions(options, resolvedModel);
        return await chatClient.GetResponseAsync(messages, effectiveOptions, cancellationToken);
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var (chatClient, resolvedModel) = await _llmManager.ResolveChatClientForCurrentContextAsync(options?.ModelId, cancellationToken);

        var effectiveOptions = BuildEffectiveOptions(options, resolvedModel);

        await foreach (var update in chatClient.GetStreamingResponseAsync(messages, effectiveOptions, cancellationToken))
        {
            yield return update;
        }
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
