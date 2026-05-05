using AgenticSystem.Core.LLM.Interfaces;
using AgenticSystem.Core.LLM.Models;
using Microsoft.Extensions.AI;
using MChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace AgenticSystem.Infrastructure.LLM;

/// <summary>
/// Adapter reverso: expõe um ILLMProvider como IChatClient.
/// </summary>
public sealed class ProviderBackedChatClient : IChatClient
{
    private readonly ILLMProvider _provider;
    private bool _disposed;

    public ProviderBackedChatClient(ILLMProvider provider)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
    }

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<MChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messages);

        var request = MapRequest(messages, options);
        var response = await _provider.GenerateAsync(request, cancellationToken);

        if (!response.Success)
        {
            throw new InvalidOperationException(response.ErrorMessage ?? $"Provider '{_provider.Name}' failed.");
        }

        var assistant = new MChatMessage(ChatRole.Assistant, response.Content);
        return new ChatResponse(assistant);
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<MChatMessage> messages,
        ChatOptions? options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var response = await GetResponseAsync(messages, options, cancellationToken);
        var text = response.Text ?? string.Empty;

        if (string.IsNullOrEmpty(text))
            yield break;

        yield return new ChatResponseUpdate(ChatRole.Assistant, text);
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

    private static LLMRequest MapRequest(IEnumerable<MChatMessage> messages, ChatOptions? options)
    {
        var request = new LLMRequest
        {
            Model = options?.ModelId,
            Parameters = new LLMParameters
            {
                Temperature = options?.Temperature ?? 0.7,
                MaxTokens = options?.MaxOutputTokens ?? 2000,
                TopP = options?.TopP ?? 1.0,
                FrequencyPenalty = options?.FrequencyPenalty ?? 0.0,
                PresencePenalty = options?.PresencePenalty ?? 0.0,
                Stop = options?.StopSequences?.ToList()
            }
        };

        foreach (var message in messages)
        {
            var role = message.Role == ChatRole.System
                ? "system"
                : message.Role == ChatRole.Assistant
                    ? "assistant"
                    : "user";

            request.Messages.Add(new Core.LLM.Models.ChatMessage
            {
                Role = role,
                Content = message.Text ?? string.Empty
            });
        }

        if (request.Messages.Count == 1 && request.Messages[0].Role == "user")
        {
            request.Prompt = request.Messages[0].Content;
        }

        return request;
    }
}
