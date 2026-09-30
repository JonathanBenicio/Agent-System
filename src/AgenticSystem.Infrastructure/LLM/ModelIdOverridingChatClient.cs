using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;

namespace AgenticSystem.Infrastructure.LLM;

/// <summary>
/// Decorador de IChatClient que intercepta e força a propriedade ModelId nas ChatOptions.
/// Usado para garantir que agentes dinâmicos utilizem modelos específicos de acordo com a etapa do workflow.
/// </summary>
public sealed class ModelIdOverridingChatClient : IChatClient
{
    private readonly IChatClient _inner;
    private readonly string _modelId;

    public ModelIdOverridingChatClient(IChatClient inner, string modelId)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);
        _inner = inner;
        _modelId = modelId;
    }

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new ChatOptions();
        options.ModelId = _modelId;
        return _inner.GetResponseAsync(messages, options, cancellationToken);
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new ChatOptions();
        options.ModelId = _modelId;
        return _inner.GetStreamingResponseAsync(messages, options, cancellationToken);
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        if (serviceType == typeof(IChatClient))
            return this;
        return _inner.GetService(serviceType, serviceKey);
    }

    public void Dispose()
    {
        _inner.Dispose();
    }
}
